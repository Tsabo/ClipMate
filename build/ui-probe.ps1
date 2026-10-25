#!/usr/bin/env pwsh
<#
.SYNOPSIS
Starts an isolated ClipMate, sets it up, drives it with keys and captures its windows. Run it through the UI-Probe
Cake target (see build/README.md), which passes the built executable.

.DESCRIPTION
1. Creates the data folder's settings by starting ClipMate once, then applies each -Setting.
2. Starts ClipMate (never capturing the clipboard at startup) and waits until clipboard monitoring runs.
3. Copies each -Copy text, then each -CopyHtml fragment (as HTML plus its plain text), waiting until ClipMate has
   saved each. This replaces what is on the user's clipboard; text that was there is put back at the end.
4. Presses each -Press key combination, -WaitMs apart.
5. Saves a PNG of each of ClipMate's visible windows into -Capture: only ClipMate's own pixels, never the desktop,
   so the images can go in a pull request.
#>

[CmdletBinding()]
Param(
    [Parameter(Mandatory)][string]$Exe,
    [string]$DataDir = (Join-Path ([IO.Path]::GetTempPath()) ("clipmate-probe-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))),
    # section.key=value, value written as TOML: preferences.initial_show_mode="Nothing"
    [string[]]$Setting = @(),
    [string[]]$Copy = @(),
    # an HTML fragment such as <a href="https://example.com">Example</a>, offered as CF_HTML and as plain text
    [string[]]$CopyHtml = @(),
    # Ctrl+Alt+Shift+F9: pick keys the user's own ClipMate does not hold, or the presses go to it
    [string[]]$Press = @(),
    [int]$WaitMs = 800,
    [string]$Capture = "",
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ProbeNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] static extern bool EmptyClipboard();
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);

    // Win32 rather than the WinForms clipboard, which needs an STA thread that pwsh doesn't have
    public static void CopyHtml(string fragment, string text)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        var utf8 = System.Text.Encoding.UTF8;
        var headerLength = string.Format(header, 0, 0, 0, 0).Length;
        var before = "<html><body><!--StartFragment-->";
        var after = "<!--EndFragment--></body></html>";
        var startFragment = headerLength + utf8.GetByteCount(before);
        var endFragment = startFragment + utf8.GetByteCount(fragment);
        var endHtml = endFragment + utf8.GetByteCount(after);
        var cfHtml = string.Format(header, headerLength, endHtml, startFragment, endFragment) + before + fragment + after;

        for (var attempt = 0; !OpenClipboard(IntPtr.Zero); attempt++)
        {
            if (attempt == 20) throw new InvalidOperationException("The clipboard is held by another program");
            System.Threading.Thread.Sleep(50);
        }

        try
        {
            EmptyClipboard();
            SetClipboardData(13, Global(System.Text.Encoding.Unicode.GetBytes(text + "\0"))); // CF_UNICODETEXT
            SetClipboardData(RegisterClipboardFormat("HTML Format"), Global(utf8.GetBytes(cfHtml + "\0")));
        }
        finally
        {
            CloseClipboard();
        }
    }

    static IntPtr Global(byte[] bytes)
    {
        var memory = GlobalAlloc(0x0002, (UIntPtr)bytes.Length); // GMEM_MOVEABLE
        Marshal.Copy(bytes, 0, GlobalLock(memory), bytes.Length);
        GlobalUnlock(memory);
        return memory;
    }

    public static List<IntPtr> VisibleWindowsOf(int processId)
    {
        var windows = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner == processId && IsWindowVisible(hWnd))
                windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}
'@
Add-Type -AssemblyName System.Drawing

# Physical pixels, so captures are sharp on a scaled display
[ProbeNative]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null

$logDir = Join-Path $DataDir "Logs"
function Read-Log {
    if (-not (Test-Path $logDir)) { return "" }
    (Get-ChildItem $logDir -Filter "clipmate-*.log" | ForEach-Object {
        $stream = [IO.FileStream]::new($_.FullName, "Open", "Read", "ReadWrite, Delete")
        try { [IO.StreamReader]::new($stream).ReadToEnd() } finally { $stream.Dispose() }
    }) -join ""
}

function Wait-Log([string]$Pattern, [int]$Count = 1, [int]$TimeoutSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ([regex]::Matches((Read-Log), $Pattern).Count -ge $Count) { return }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for '$Pattern' in $logDir"
}

function Start-ClipMate {
    Start-Process $Exe -ArgumentList "--data-dir `"$DataDir`" --no-startup-capture" -PassThru
}

New-Item -ItemType Directory -Force $DataDir | Out-Null
Write-Host "Data dir: $DataDir"

# 1. Settings: let ClipMate write its defaults, then change the requested keys
$settingsPath = Join-Path $DataDir "clipmate.toml"
if (-not (Test-Path $settingsPath)) {
    $first = Start-ClipMate
    try { Wait-Log "Initialization pipeline completed" } finally { Stop-Process -Id $first.Id -Force; $first.WaitForExit() }
}

if ($Setting.Count -gt 0) {
    $lines = [Collections.Generic.List[string]](Get-Content $settingsPath)
    foreach ($item in $Setting) {
        if ($item -notmatch '^(?<section>[\w.]+)\.(?<key>\w+)=(?<value>.+)$') { throw "Setting '$item' is not section.key=value" }
        $header = "[$($Matches.section)]"; $key = $Matches.key; $line = "$key = $($Matches.value)"
        $start = $lines.IndexOf($header)
        if ($start -lt 0) { $lines.Add($header); $lines.Add($line); continue }
        $index = $start + 1; $replaced = $false
        while ($index -lt $lines.Count -and -not $lines[$index].StartsWith("[")) {
            if ($lines[$index] -match "^$key\s*=") { $lines[$index] = $line; $replaced = $true; break }
            $index++
        }
        if (-not $replaced) { $lines.Insert($start + 1, $line) }
    }
    Set-Content $settingsPath $lines
}

# 2. Start and wait for clipboard monitoring
Get-ChildItem $logDir -ErrorAction SilentlyContinue | Remove-Item -Force
$app = Start-ClipMate
$savedText = Get-Clipboard -Raw -ErrorAction SilentlyContinue
try {
    Wait-Log "Clipboard coordinator started successfully"
    Write-Host "ClipMate running (PID $($app.Id))"

    # 3. Copies, each saved before the next
    $saved = [regex]::Matches((Read-Log), "Clip saved successfully").Count
    foreach ($text in $Copy) {
        Set-Clipboard -Value $text
        $saved++
        Wait-Log "Clip saved successfully" -Count $saved -TimeoutSeconds 15
    }
    foreach ($fragment in $CopyHtml) {
        [ProbeNative]::CopyHtml($fragment, ($fragment -replace '<[^>]+>', ''))
        $saved++
        Wait-Log "Clip saved successfully" -Count $saved -TimeoutSeconds 15
    }

    # 4. Key presses
    $keys = @{ Ctrl = 0x11; Alt = 0x12; Shift = 0x10; Win = 0x5B; Enter = 0x0D; Esc = 0x1B; Tab = 0x09; Space = 0x20; Up = 0x26; Down = 0x28; Left = 0x25; Right = 0x27 }
    1..24 | ForEach-Object { $keys["F$_"] = 0x6F + $_ }
    foreach ($combination in $Press) {
        $codes = $combination.Split("+") | ForEach-Object {
            if ($keys.ContainsKey($_)) { [byte]$keys[$_] }
            elseif ($_ -match '^[A-Z0-9]$') { [byte][char]$_ }
            else { throw "Unknown key '$_' in '$combination'" }
        }
        foreach ($code in $codes) { [ProbeNative]::keybd_event($code, 0, 0, [UIntPtr]::Zero) }
        [array]::Reverse($codes)
        foreach ($code in $codes) { [ProbeNative]::keybd_event($code, 0, 2, [UIntPtr]::Zero) }
        Start-Sleep -Milliseconds $WaitMs
    }

    # 5. Captures of ClipMate's own windows
    if ($Capture) {
        New-Item -ItemType Directory -Force $Capture | Out-Null
        $index = 0
        foreach ($window in [ProbeNative]::VisibleWindowsOf($app.Id)) {
            $rect = New-Object ProbeNative+RECT
            [ProbeNative]::GetWindowRect($window, [ref]$rect) | Out-Null
            $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
            if ($width -le 1 -or $height -le 1) { continue }

            $title = New-Object Text.StringBuilder 256
            [ProbeNative]::GetWindowText($window, $title, 256) | Out-Null
            $name = ($title.ToString() -replace '[^\w-]+', '-').Trim('-')
            $path = Join-Path $Capture ("window-{0}{1}.png" -f ++$index, $(if ($name) { "-$name" } else { "" }))

            $bitmap = New-Object Drawing.Bitmap $width, $height
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            $hdc = $graphics.GetHdc()
            [ProbeNative]::PrintWindow($window, $hdc, 2) | Out-Null # PW_RENDERFULLCONTENT
            $graphics.ReleaseHdc($hdc); $graphics.Dispose()
            $bitmap.Save($path); $bitmap.Dispose()
            Write-Host "Captured $path (${width}x$height)"
        }
    }
}
finally {
    if ($Copy.Count + $CopyHtml.Count -gt 0 -and $savedText) { Set-Clipboard -Value $savedText }
    if ($KeepRunning) { Write-Host "Left running. Stop it with: Stop-Process -Id $($app.Id)" }
    else { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    Write-Host "Log: $logDir"
}
