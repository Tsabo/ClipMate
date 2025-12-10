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
var versionOverride = Argument("version", "");

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

// Publish output directory
var publishDir = buildDir.Combine($"publish/{configuration}");

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

Task("Build")
    .IsDependentOn("Restore")
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
    
    DotNetTest(solutionFile.FullPath, new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        NoRestore = true,
        Loggers = new[] { $"trx;LogFileName=test-results-{DateTime.Now:yyyyMMdd-HHmmss}.trx" },
        ResultsDirectory = testResultsDir
    });
    
    var elapsed = DateTime.Now - startTime;
    Information($"Tests completed in {elapsed.TotalSeconds:F2}s");
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
    
    foreach (var logFile in logFiles)
    {
        try
        {
            var content = System.IO.File.ReadAllText(logFile.FullPath);
            
            // Sanitize paths
            content = content.Replace(repoRoot.FullPath, "<REPO_ROOT>");
            content = content.Replace(userProfile, "<USER_PROFILE>");
            content = Regex.Replace(content, @"C:\\Users\\[^\\]+", "C:\\Users\\<USER>");
            
            // Sanitize username
            content = content.Replace(username, "<USER>");
            
            System.IO.File.WriteAllText(logFile.FullPath, content);
        }
        catch (Exception ex)
        {
            Warning($"Failed to sanitize {logFile.GetFilename()}: {ex.Message}");
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
    Information("Generating checksums...");
    
    var installers = GetFiles($"{installerOutputDir}/*.exe");
    
    foreach (var installer in installers)
    {
        var checksumFile = File($"{installer.ToString()}.sha256");
        var hash = CalculateFileHash(installer, HashAlgorithm.SHA256);
        
        System.IO.File.WriteAllText(checksumFile.ToString(), 
            $"{hash.ToHex()}  {installer.GetFilename()}");
        
        Information($"Checksum: {installer.GetFilename()}.sha256");
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
