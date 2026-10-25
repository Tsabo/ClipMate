//////////////////////////////////////////////////////////////////////
// ClipMate Build Script
//////////////////////////////////////////////////////////////////////

#addin nuget:?package=Cake.MinVer&version=4.0.0

using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
///
var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var versionOverride = Argument("release-version", "");
// Builds into this folder instead of each project's bin (used when a running ClipMate locks bin)
var artifactsPath = Argument("artifacts-path", "");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////
///
var buildDir = MakeAbsolute(Directory("./"));
var repoRoot = buildDir.Combine("../");
var sourceDir = repoRoot.Combine("Source");
var cacheDir = buildDir.Combine("cache");
var logsDir = buildDir.Combine("logs");
var installerDir = buildDir.Combine("installer");
var installerOutputDir = installerDir.Combine("output");
var solutionFile = sourceDir.CombineWithFilePath("ClipMate.sln");
var appProject = sourceDir.Combine("src/ClipMate.App").CombineWithFilePath("ClipMate.App.csproj");

// Font building paths
var fontsDir = repoRoot.Combine("Resources/Fonts");
var fontBuildScript = fontsDir.CombineWithFilePath("build_color_font.py");
var fontConfig = fontsDir.CombineWithFilePath("config.json");
var fontSvgDir = fontsDir.Combine("svgs");
var fontOutput = fontsDir.CombineWithFilePath("ClipMate.ttf");
var fontAssetsDir = sourceDir.Combine("src/ClipMate.App/Assets");

// Publish output directory
var publishDir = buildDir.Combine($"publish/{configuration}");
var publishSingleFileDir = buildDir.Combine($"publish-singlefile/{configuration}");

// Version information
string version = "";
bool isPreRelease = false;

// DevExpress NuGet configuration
var devExpressAuthKey = EnvironmentVariable("DEVEXPRESS_FEED_AUTH_KEY");
var devExpressFeedUrl = !string.IsNullOrEmpty(devExpressAuthKey) 
    ? $"https://nuget.devexpress.com/{devExpressAuthKey}/api/v3/index.json"
    : null;

// SignPath configuration
var signPathApiToken = EnvironmentVariable("SIGNPATH_API_TOKEN");
var signPathOrgId = EnvironmentVariable("SIGNPATH_ORGANIZATION_ID");
var signPathProjectSlug = EnvironmentVariable("SIGNPATH_PROJECT_SLUG") ?? "ClipMate";
var signPathPolicySlug = EnvironmentVariable("SIGNPATH_SIGNING_POLICY") ?? "release-signing";
bool canSign = !string.IsNullOrEmpty(signPathApiToken) && !string.IsNullOrEmpty(signPathOrgId);

//////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
//////////////////////////////////////////////////////////////////////
///
Setup(ctx =>
{
    Information("========================================");
    Information("ClipMate Build");
    Information("========================================");
    Information($"Target: {target}");
    Information($"Configuration: {configuration}");
    Information($"Repository Root: {repoRoot}");
    Information("");
    
    // Restore .NET tools if needed
    Information("Restoring .NET tools...");
    DotNetTool("tool restore", new DotNetToolSettings
    {
        WorkingDirectory = repoRoot
    });
    Information("Tools restored successfully");
    Information("");
    
    // Ensure directories exist
    EnsureDirectoryExists(cacheDir);
    EnsureDirectoryExists(logsDir);
    EnsureDirectoryExists(installerOutputDir);
});

Teardown(ctx =>
{
    Information("");
    Information("========================================");
    if (ctx.Successful)
    {
        Information("Build completed successfully!");
    }
    else
    {
        Error("Build failed!");
    }
    Information("========================================");
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Cleaning build artifacts...");
    
    CleanDirectory(buildDir.Combine("publish"));
    CleanDirectory(buildDir.Combine("publish-singlefile"));
    CleanDirectory(installerOutputDir);
    CleanDirectory(logsDir);
    
    // Clean solution
    DotNetClean(solutionFile.FullPath, new DotNetCleanSettings
    {
        Configuration = configuration
    });
    
    var elapsed = DateTime.Now - startTime;
    Information($"Clean completed in {elapsed.TotalSeconds:F2}s");
});

Task("Determine-Version")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Determining version...");
    
    if (!string.IsNullOrEmpty(versionOverride))
    {
        version = versionOverride;
        Information($"Using override version: {version}");
    }
    else
    {
        try
        {
            // Use MinVer to calculate version from git tags
            var minVerVersion = MinVer(new MinVerSettings
            {
                WorkingDirectory = repoRoot.ToString(),
                DefaultPreReleasePhase = "dev"
            });
            
            version = minVerVersion.Version;
            Information($"MinVer calculated version: {version}");
        }
        catch (Exception ex)
        {
            version = "0.1.0-dev";
            Warning($"Could not determine version from git: {ex.Message}");
            Warning($"Using fallback: {version}");
        }
    }
    
    // Check if pre-release
    isPreRelease = version.Contains("-");
    
    Information($"Final version: {version}");
    Information($"Pre-release: {isPreRelease}");
    
    var elapsed = DateTime.Now - startTime;
    Information($"Version determination completed in {elapsed.TotalSeconds:F2}s");
});

Task("Restore")
    .IsDependentOn("Determine-Version")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Restoring NuGet packages...");
    
    // Add DevExpress feed if available
    if (!string.IsNullOrEmpty(devExpressFeedUrl))
    {
        Information("DevExpress NuGet feed configured");
        
        // Check if source already exists
        var sources = NuGetHasSource("DevExpress Online");
        
        if (sources)
        {
            Information("DevExpress Online source already configured - updating...");
            
            // Remove and re-add to update URL with authentication
            NuGetRemoveSource(devExpressFeedUrl, "DevExpress Online", new NuGetSourcesSettings
            {
                WorkingDirectory = repoRoot
            });
        }
        
        // Add DevExpress source with authentication
        Information("Adding DevExpress Online source...");
        NuGetAddSource("DevExpress Online", devExpressFeedUrl, new NuGetSourcesSettings
        {
            WorkingDirectory = repoRoot
        });
    }
    else
    {
        Warning("DEVEXPRESS_FEED_AUTH_KEY not set - DevExpress packages may fail to restore");
    }
    
    // Restore packages
    DotNetRestore(solutionFile.FullPath);
    
    var elapsed = DateTime.Now - startTime;
    Information($"Restore completed in {elapsed.TotalSeconds:F2}s");
});

Task("Build-Font")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Building custom color font...");
    
    // Check if Python is available
    try
    {
        var pythonVersion = StartProcess("python", new ProcessSettings
        {
            Arguments = "--version",
            RedirectStandardOutput = true
        });
        
        if (pythonVersion != 0)
        {
            throw new Exception("Python not found");
        }
    }
    catch
    {
        Warning("Python not found - skipping font build");
        Warning("Install Python 3.11+ to build custom fonts");
        return;
    }
    
    // Check if Ninja is available
    try
    {
        var ninjaVersion = StartProcess("ninja", new ProcessSettings
        {
            Arguments = "--version",
            RedirectStandardOutput = true
        });
        
        if (ninjaVersion != 0)
        {
            throw new Exception("Ninja not found");
        }
    }
    catch
    {
        Warning("Ninja build tool not found - skipping font build");
        Warning("Install Ninja (https://github.com/ninja-build/ninja/releases) to build custom fonts");
        return;
    }
    
    // Install Python dependencies
    Information("Installing Python dependencies...");
    var pipInstall = StartProcess("python", new ProcessSettings
    {
        Arguments = "-m pip install --quiet nanoemoji fonttools",
        WorkingDirectory = fontsDir
    });
    
    if (pipInstall != 0)
    {
        Warning("Failed to install Python dependencies - skipping font build");
        return;
    }
    
    // Build the font
    Information("Running font build script...");
    var fontBuild = StartProcess("python", new ProcessSettings
    {
        Arguments = $"build_color_font.py config.json ./svgs/ ClipMate.ttf",
        WorkingDirectory = fontsDir
    });
    
    if (fontBuild != 0)
    {
        throw new Exception("Font build failed");
    }
    
    // Copy font to Assets directory
    if (!FileExists(fontOutput))
    {
        throw new Exception($"Font output not found: {fontOutput}");
    }
    
    Information("Copying font to Assets directory...");
    EnsureDirectoryExists(fontAssetsDir);
    CopyFile(fontOutput, fontAssetsDir.CombineWithFilePath("ClipMate.ttf"));
    
    var fontSize = new System.IO.FileInfo(fontOutput.FullPath).Length;
    Information($"Font built successfully: {fontSize / 1024:F2} KB");
    
    var elapsed = DateTime.Now - startTime;
    Information($"Font build completed in {elapsed.TotalSeconds:F2}s");
})
.OnError(exception =>
{
    Warning($"Font build failed: {exception.Message}");
    Warning("Continuing without custom font...");
});

Task("Build")
    .IsDependentOn("Restore")
    .IsDependentOn("Build-Font")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Building solution...");
    
    var msbuildLog = logsDir.CombineWithFilePath($"build-{DateTime.Now:yyyyMMdd-HHmmss}.binlog");
    
    DotNetBuild(solutionFile.FullPath, new DotNetBuildSettings
    {
        Configuration = configuration,
        NoRestore = true,
        MSBuildSettings = new DotNetMSBuildSettings()
            .SetVersion(version)
            .WithProperty("AssemblyVersion", version.Split('-')[0] + ".0")
            .WithProperty("FileVersion", version.Split('-')[0] + ".0")
            .WithProperty("InformationalVersion", version)
            .SetMaxCpuCount(0)
            .AddFileLogger(new MSBuildFileLoggerSettings
            {
                LogFile = msbuildLog.FullPath
            })
    });
    
    var elapsed = DateTime.Now - startTime;
    Information($"Build completed in {elapsed.TotalSeconds:F2}s");
    Information($"Build log: {msbuildLog}");
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Running tests...");
    
    var testResultsDir = logsDir.Combine("test-results");
    EnsureDirectoryExists(testResultsDir);
    
    // Use StartProcess to call dotnet test directly because Cake's DotNetTest() 
    // doesn't work correctly with .NET 10 SDK MTP mode argument parsing
    var exitCode = StartProcess("dotnet", new ProcessSettings
    {
        Arguments = $"test --configuration {configuration} --no-build --no-restore --report-trx --results-directory \"{testResultsDir.FullPath}\"",
        WorkingDirectory = sourceDir
    });
    
    if (exitCode != 0)
    {
        throw new Exception($"Tests failed with exit code {exitCode}");
    }
    
    var elapsed = DateTime.Now - startTime;
    Information($"Tests completed in {elapsed.TotalSeconds:F2}s");
});

// Where a project's build put its executable: its own bin, or the --artifacts-path layout
FilePath BuiltExecutable(FilePath project)
{
    var name = project.GetFilenameWithoutExtension().FullPath;
    var folder = string.IsNullOrEmpty(artifactsPath)
        ? project.GetDirectory().Combine($"bin/{configuration}/net10.0-windows")
        : MakeAbsolute(Directory(artifactsPath)).Combine($"bin/{name}/{configuration.ToLowerInvariant()}");

    return folder.CombineWithFilePath($"{name}.exe");
}

Task("Smoke-Test")
    .Description("Start ClipMate against an isolated data folder and check that it gets through startup")
    .Does(() =>
{
    // --exe (default: the configuration's build), --data-dir (default: new folder in %TEMP%; reuse one to keep its
    // settings), --timeout seconds, --keep-running (leave it running, e.g. to pair two copies). The copy never captures
    // what is on the user's clipboard at startup (--no-startup-capture).
    var exe = Argument("exe", BuiltExecutable(appProject).FullPath);
    var dataDir = Argument("data-dir", System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clipmate-smoke-{Guid.NewGuid():N}"[..23]));
    var timeout = TimeSpan.FromSeconds(Argument("timeout", 90));
    var keepRunning = HasArgument("keep-running");

    if (!FileExists(exe))
        throw new Exception($"{exe} not found: build it first, or pass --exe");

    EnsureDirectoryExists(dataDir);
    var logDir = System.IO.Path.Combine(dataDir, "Logs");
    Information($"ClipMate: {exe}");
    Information($"Data dir: {dataDir}");

    // Shell-launched like a user would, so the app's console output stays out of the build log
    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, $"--data-dir \"{dataDir}\" --no-startup-capture") { UseShellExecute = true });

    // The early and configured loggers roll to separate files, so read them all; the app still has them open
    string ReadLog() => !System.IO.Directory.Exists(logDir)
        ? string.Empty
        : string.Concat(System.IO.Directory.EnumerateFiles(logDir, "clipmate-*.log").Select(p =>
        {
            using var reader = new System.IO.StreamReader(new System.IO.FileStream(p, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete));
            return reader.ReadToEnd();
        }));

    string result = null;
    var deadline = DateTime.UtcNow + timeout;
    while (result == null && DateTime.UtcNow < deadline)
    {
        System.Threading.Thread.Sleep(500);
        var log = ReadLog();
        if (log.Contains("Initialization pipeline completed"))
            result = "started";
        else if (Regex.IsMatch(log, @"\[FTL\]|Fatal error during application startup|Failed to build host"))
            result = "fatal";
        else if (process.HasExited)
            result = "exited";
    }

    if (result != "started")
    {
        result ??= "timeout";
        Error($"SMOKE TEST FAILED: {result}");
        Information("--- last log lines ---");
        foreach (var item in ReadLog().Split('\n').TakeLast(40))
            Information(item.TrimEnd());

        if (result == "timeout" && !process.HasExited)
        {
            // Managed thread stacks show where startup hangs
            Information("--- managed thread stacks (dotnet-stack) ---");
            StartProcess("dotnet", new ProcessSettings { Arguments = $"tool run dotnet-stack report -p {process.Id}", WorkingDirectory = repoRoot });
        }

        if (!process.HasExited)
            process.Kill(true);

        throw new Exception($"Smoke test failed: {result}");
    }

    Information($"SMOKE TEST PASSED: startup completed (PID {process.Id})");
    if (keepRunning)
        Information($"Left running. Stop it with: Stop-Process -Id {process.Id}");
    else
        process.Kill(true);
});

Task("UI-Probe")
    .Description("Start an isolated ClipMate with given settings, copy text, press keys and capture its windows (build/ui-probe.ps1)")
    .Does(() =>
{
    // --setting section.key=value, --copy text, --press Ctrl+Alt+Shift+F9 (each repeatable, in order), --wait-ms,
    // --capture folder, --data-dir, --exe, --keep-running
    string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    var command = new System.Text.StringBuilder("& ")
        .Append(Quote(MakeAbsolute(buildDir.CombineWithFilePath("ui-probe.ps1")).FullPath))
        .Append(" -Exe ").Append(Quote(Argument("exe", BuiltExecutable(appProject).FullPath)));

    foreach (var (name, parameter) in new[] { ("setting", "-Setting"), ("copy", "-Copy"), ("press", "-Press") })
    {
        var values = Arguments<string>(name, Array.Empty<string>());
        if (values.Count > 0)
            command.Append(' ').Append(parameter).Append(' ').Append(string.Join(",", values.Select(Quote)));
    }

    if (HasArgument("wait-ms"))
        command.Append(" -WaitMs ").Append(Argument<int>("wait-ms"));
    if (HasArgument("capture"))
        command.Append(" -Capture ").Append(Quote(MakeAbsolute(Directory(Argument<string>("capture"))).FullPath));
    if (HasArgument("data-dir"))
        command.Append(" -DataDir ").Append(Quote(Argument<string>("data-dir")));
    if (HasArgument("keep-running"))
        command.Append(" -KeepRunning");

    // -File would take each list as one string; an encoded command passes the arrays without any quoting rules
    var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command.ToString()));
    var exitCode = StartProcess("pwsh", new ProcessSettings
    {
        Arguments = new ProcessArgumentBuilder().Append("-NoProfile").Append("-NonInteractive")
            .Append("-OutputFormat").Append("Text").Append("-EncodedCommand").Append(encoded),
        WorkingDirectory = repoRoot,
    });
    if (exitCode != 0)
        throw new Exception($"UI-Probe failed (exit code {exitCode})");
});

Task("Prepare-Worktree")
    .Description("Make a fresh git worktree buildable: copy the gitignored font and Monaco assets from the main checkout and link its local-only docs")
    .Does(() =>
{
    // --main-checkout (default: the checkout that owns this worktree's git directory, i.e. <main checkout>\.git)
    var worktree = System.IO.Path.GetFullPath(repoRoot.FullPath).TrimEnd('\\', '/');
    var mainCheckout = Argument("main-checkout", "");
    if (string.IsNullOrEmpty(mainCheckout))
    {
        StartProcess("git", new ProcessSettings
        {
            Arguments = "rev-parse --path-format=absolute --git-common-dir",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
        }, out var output);

        mainCheckout = System.IO.Path.GetDirectoryName(output.First().Trim());
    }

    mainCheckout = System.IO.Path.GetFullPath(mainCheckout).TrimEnd('\\', '/');
    if (string.Equals(mainCheckout, worktree, StringComparison.OrdinalIgnoreCase))
    {
        Information("This is the main checkout; nothing to copy.");
        return;
    }

    // ClipMate.ttf is built by Build-Font; Monaco would otherwise be downloaded
    foreach (var item in new[] { "Source/src/ClipMate.App/Assets/ClipMate.ttf", "Source/src/ClipMate.App/Assets/monaco/min" })
    {
        var target = System.IO.Path.Combine(worktree, item);
        var source = System.IO.Path.Combine(mainCheckout, item);
        if (FileExists(target) || DirectoryExists(target))
            Information($"present   {item}");
        else if (FileExists(source))
        {
            CopyFile(source, target);
            Information($"copied    {item}");
        }
        else if (DirectoryExists(source))
        {
            CopyDirectory(source, target);
            Information($"copied    {item}");
        }
        else
            Warning($"missing   {item} (not in {mainCheckout} either; build the main checkout first)");
    }

    // Local-only documents (listed in .git/info/exclude, so never in a worktree): link them to the main checkout's copies, so
    // relative paths resolve and edits (a ticket's status, say) land in the one real copy. Folders become junctions and files
    // hard links, since neither needs the symlink privilege. CLAUDE.md is skipped when the worktree sits inside the main
    // checkout, which already loads it as a parent folder's CLAUDE.md.
    var insideMainCheckout = worktree.StartsWith(mainCheckout + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    var localDocs = new[] { "CLAUDE.md", "CODING_STANDARDS.md", "GLOSSARY.md", "adr", "specs", ".scratch", "docs/agents" }
        .Where(p => !(insideMainCheckout && p == "CLAUDE.md"));

    Information("");
    foreach (var item in localDocs)
    {
        var target = System.IO.Path.Combine(worktree, item);
        var source = System.IO.Path.Combine(mainCheckout, item);
        if (FileExists(target) || DirectoryExists(target))
        {
            Information($"present   {item}");
            continue;
        }

        var isDirectory = DirectoryExists(source);
        if (!isDirectory && !FileExists(source))
            continue;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
        var exitCode = StartProcess("cmd", new ProcessSettings
        {
            Arguments = $"/c mklink {(isDirectory ? "/J" : "/H")} \"{target}\" \"{source}\"",
            RedirectStandardOutput = true,
        });

        if (exitCode == 0)
            Information($"linked    {item}");
        else
            Warning($"not linked {item} (mklink exit {exitCode}); read it from {source}");
    }
});

Task("UI-Test")
    .Description("Run the UI tests (not part of CI): each starts an isolated ClipMate and drives the real clipboard")
    .Does(() =>
{
    // --test-filter narrows the run with a TUnit tree filter, e.g. "/*/*/ClipboardFileDropRoundTripTests/*"
    var uiTestsProject = sourceDir.CombineWithFilePath("tests/ClipMate.UITests/ClipMate.UITests.csproj");
    var filter = Argument("test-filter", "");
    var fullOutput = HasArgument("full-output");

    DotNetBuild(uiTestsProject.FullPath, new DotNetBuildSettings
    {
        Configuration = configuration,
        Verbosity = DotNetVerbosity.Quiet,
        ArgumentCustomization = args => string.IsNullOrEmpty(artifactsPath)
            ? args
            : args.Append("--artifacts-path").AppendQuoted(MakeAbsolute(Directory(artifactsPath)).FullPath),
    });

    var exe = BuiltExecutable(uiTestsProject);
    var processSettings = new ProcessSettings
    {
        Arguments = string.IsNullOrEmpty(filter) ? "" : $"--treenode-filter \"{filter}\"",
        WorkingDirectory = exe.GetDirectory(),
        RedirectStandardOutput = !fullOutput,
        RedirectStandardError = !fullOutput,
    };

    if (fullOutput)
    {
        if (StartProcess(exe, processSettings) != 0)
            throw new Exception("UI tests failed");

        return;
    }

    // The full output runs to thousands of lines; print the outcome and each failure's first lines, keep the rest in a log
    var exitCode = StartProcess(exe, processSettings, out var standardOutput, out var standardError);
    var lines = standardOutput.Concat(standardError).ToList();
    EnsureDirectoryExists(logsDir);
    var log = logsDir.CombineWithFilePath($"ui-test-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    System.IO.File.WriteAllLines(log.FullPath, lines);

    for (var i = 0; i < lines.Count; i++)
    {
        var line = lines[i].Trim();
        if (!line.StartsWith("failed ", StringComparison.Ordinal))
            continue;

        Information(line);
        foreach (var detail in lines.Skip(i + 1).Take(12).Select(p => p.Trim())
                     .Where(p => p.Contains("Exception", StringComparison.Ordinal) || p.StartsWith("Expected", StringComparison.Ordinal) ||
                                 p.StartsWith("but ", StringComparison.Ordinal) || p.StartsWith("at ClipMate.UITests.Tests", StringComparison.Ordinal))
                     .Take(4))
            Information($"    {detail}");
    }

    foreach (var line in lines.Select(p => p.Trim()).Where(p => p.StartsWith("total:") || p.StartsWith("failed:") ||
                                                                 p.StartsWith("succeeded:") || p.StartsWith("skipped:")))
        Information(line);

    Information($"Full output: {log.FullPath}");
    if (exitCode != 0)
        throw new Exception($"UI tests failed with exit code {exitCode}");
});

Task("Publish")
    .IsDependentOn("Build")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Publishing framework-dependent application...");
    
    CleanDirectory(publishDir);
    
    DotNetPublish(appProject.FullPath, new DotNetPublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishDir,
        SelfContained = false,
        Runtime = null, // Framework-dependent
        MSBuildSettings = new DotNetMSBuildSettings()
            .SetVersion(version)
            .WithProperty("AssemblyVersion", version.Split('-')[0] + ".0")
            .WithProperty("FileVersion", version.Split('-')[0] + ".0")
            .WithProperty("InformationalVersion", version)
    });
    
    // Count files and calculate size
    var files = GetFiles($"{publishDir}/**/*");
    long totalSize = files.Sum(f => new System.IO.FileInfo(f.FullPath).Length);
    
    Information($"Published {files.Count} files ({totalSize / 1024 / 1024:F2} MB)");
    
    var elapsed = DateTime.Now - startTime;
    Information($"Framework-dependent publish completed in {elapsed.TotalSeconds:F2}s");
});

Task("Publish-SingleFile")
    .IsDependentOn("Build")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Publishing self-contained single-file application...");
    
    CleanDirectory(publishSingleFileDir);
    
    DotNetPublish(appProject.FullPath, new DotNetPublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishSingleFileDir,
        SelfContained = true,
        Runtime = "win-x64",
        MSBuildSettings = new DotNetMSBuildSettings()
            .SetVersion(version)
            .WithProperty("AssemblyVersion", version.Split('-')[0] + ".0")
            .WithProperty("FileVersion", version.Split('-')[0] + ".0")
            .WithProperty("InformationalVersion", version)
            .WithProperty("PublishSingleFile", "true")
            .WithProperty("IncludeNativeLibrariesForSelfExtract", "true")
            .WithProperty("EnableCompressionInSingleFile", "true")
    });
    
    // Find the exe file
    var exeFile = GetFiles($"{publishSingleFileDir}/*.exe").FirstOrDefault();
    if (exeFile != null)
    {
        var exeSize = new System.IO.FileInfo(exeFile.FullPath).Length;
        Information($"Single-file executable: {exeFile.GetFilename()}");
        Information($"Size: {exeSize / 1024 / 1024:F2} MB");
        Information($"Output directory: {publishSingleFileDir}");
    }
    else
    {
        Warning("Could not find .exe file in output directory");
    }
    
    var elapsed = DateTime.Now - startTime;
    Information($"Single-file publish completed in {elapsed.TotalSeconds:F2}s");
});

Task("Build-Installer")
    .IsDependentOn("Publish")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Building installer...");
    
    var issScript = installerDir.CombineWithFilePath("ClipMate.iss");
    var outputName = $"ClipMate-Setup-{version}.exe";
    
    InnoSetup(issScript.FullPath, new InnoSetupSettings
    {
        Defines = new Dictionary<string, string>
        {
            { "MyAppVersion", version },
            { "SourcePath", publishDir.FullPath },
            { "OutputFilename", outputName.Replace(".exe", "") }
        },
        OutputDirectory = installerOutputDir.FullPath
    });
    
    var installerPath = installerOutputDir.CombineWithFilePath(outputName);
    var installerSize = new System.IO.FileInfo(installerPath.FullPath).Length;
    
    Information($"Installer created: {installerPath}");
    Information($"Size: {installerSize / 1024 / 1024:F2} MB");
    
    var elapsed = DateTime.Now - startTime;
    Information($"Framework installer build completed in {elapsed.TotalSeconds:F2}s");
});

Task("Sign-Installer")
    .WithCriteria(() => canSign)
    .IsDependentOn("Build-Installer")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Signing installer with SignPath...");
    
    Warning("SignPath integration not yet implemented - requires SignPath CLI or REST API");
    Warning("Installer will remain unsigned");
    
    // TODO: Implement SignPath signing when approved
    // 1. Submit installers to SignPath
    // 2. Wait for signing to complete
    // 3. Download signed installers
    
    var elapsed = DateTime.Now - startTime;
    Information($"Signing process completed in {elapsed.TotalSeconds:F2}s");
})
.OnError(exception =>
{
    Warning("Signing failed - continuing with unsigned installer");
    Warning($"Error: {exception.Message}");
});

Task("Build-Docs")
    .IsDependentOn("Build-Font")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Building Docusaurus documentation...");
    
    var docsDir = repoRoot.Combine("docs");
    var docsStaticFonts = docsDir.Combine("static/fonts");
    var docsBuildDir = docsDir.Combine("build");
    
    // Ensure font is built and copied to static directory
    Information("Copying font to docs static directory...");
    EnsureDirectoryExists(docsStaticFonts);
    
    if (FileExists(fontOutput))
    {
        CopyFile(fontOutput, docsStaticFonts.CombineWithFilePath("ClipMate.ttf"));
        Information($"Font copied: {fontOutput} -> docs/static/fonts/");
    }
    else
    {
        Warning("Font not found - documentation may be missing custom font");
    }
    
    // Install npm dependencies
    Information("Installing npm dependencies...");
    var npmInstall = StartProcess("cmd", new ProcessSettings
    {
        Arguments = "/c npm ci",
        WorkingDirectory = docsDir
    });
    
    if (npmInstall != 0)
    {
        throw new Exception("npm install failed");
    }
    
    // Build Docusaurus site
    Information("Building Docusaurus site...");
    var npmBuild = StartProcess("cmd", new ProcessSettings
    {
        Arguments = "/c npm run build",
        WorkingDirectory = docsDir
    });
    
    if (npmBuild != 0)
    {
        throw new Exception("Docusaurus build failed");
    }
    
    // Report build output
    if (DirectoryExists(docsBuildDir))
    {
        var files = GetFiles($"{docsBuildDir}/**/*");
        long totalSize = files.Sum(f => new System.IO.FileInfo(f.FullPath).Length);
        Information($"Documentation built: {files.Count} files ({totalSize / 1024 / 1024:F2} MB)");
    }
    
    var elapsed = DateTime.Now - startTime;
    Information($"Documentation build completed in {elapsed.TotalSeconds:F2}s");
})
.OnError(exception =>
{
    Warning($"Documentation build failed: {exception.Message}");
    Warning("Continuing without documentation build...");
});

Task("Sanitize-Logs")
    .Does(() =>
{
    var startTime = DateTime.Now;
    Information("Sanitizing build logs...");
    
    var username = Environment.UserName;
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    var logFiles = GetFiles($"{logsDir}/**/*.binlog") 
        .Concat(GetFiles($"{logsDir}/**/*.log"))
        .Concat(GetFiles($"{logsDir}/**/*.trx"));
    
    foreach (var item in logFiles)
    {
        try
        {
            var content = System.IO.File.ReadAllText(item.FullPath);
            
            // Sanitize paths
            content = content.Replace(repoRoot.FullPath, "<REPO_ROOT>");
            content = content.Replace(userProfile, "<USER_PROFILE>");
            content = Regex.Replace(content, @"C:\\Users\\[^\\]+", "C:\\Users\\<USER>");
            
            // Sanitize username
            content = content.Replace(username, "<USER>");
            
            System.IO.File.WriteAllText(item.FullPath, content);
        }
        catch (Exception ex)
        {
            Warning($"Failed to sanitize {item.GetFilename()}: {ex.Message}");
        }
    }
    
    Information($"Sanitized {logFiles.Count()} log files");
    
    var elapsed = DateTime.Now - startTime;
    Information($"Log sanitization completed in {elapsed.TotalSeconds:F2}s");
});

Task("Package")
    .IsDependentOn("Build-Installer")
    .Does(() =>
{
    var startTime = DateTime.Now;
    
    // Create portable ZIP
    Information("Creating portable ZIP...");
    var zipName = $"ClipMate-Portable-{version}.zip";
    var zipPath = installerOutputDir.CombineWithFilePath(zipName);
    
    // Delete existing ZIP if present
    if (FileExists(zipPath))
        DeleteFile(zipPath);
    
    Zip(publishDir, zipPath);
    
    var zipSize = new System.IO.FileInfo(zipPath.FullPath).Length;
    Information($"Portable ZIP created: {zipPath}");
    Information($"Size: {zipSize / 1024 / 1024:F2} MB");
    
    // Generate checksums for all artifacts
    Information("Generating checksums...");
    
    var artifacts = GetFiles($"{installerOutputDir}/*.exe")
        .Concat(GetFiles($"{installerOutputDir}/*.zip"));
    
    foreach (var item in artifacts)
    {
        var checksumFile = File($"{item.ToString()}.sha256");
        var hash = CalculateFileHash(item, HashAlgorithm.SHA256);
        
        System.IO.File.WriteAllText(checksumFile.ToString(), 
            $"{hash.ToHex()}  {item.GetFilename()}");
        
        Information($"Checksum: {item.GetFilename()}.sha256");
    }
    
    var elapsed = DateTime.Now - startTime;
    Information($"Packaging completed in {elapsed.TotalSeconds:F2}s");
});

//////////////////////////////////////////////////////////////////////
// META TASKS
//////////////////////////////////////////////////////////////////////
///
Task("CI")
    .Description("Continuous Integration build")
    .IsDependentOn("Clean")
    .IsDependentOn("Test")
    .IsDependentOn("Sanitize-Logs");

Task("Release")
    .Description("Full release build with installer")
    .IsDependentOn("Clean")
    .IsDependentOn("Test")
    .IsDependentOn("Build-Installer")
    .IsDependentOn("Sign-Installer")
    .IsDependentOn("Package")
    .IsDependentOn("Sanitize-Logs");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
///
RunTarget(target);
