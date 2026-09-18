# =====================================================================
# Build and Publish Release for Google Drive and Telegram Sender
# =====================================================================

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "   Build Release: Google Drive and Telegram      " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Stop running processes to release file locks
$processes = Get-Process -Name "GDriveTelegramSender" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "`n[!] Stopping running GDriveTelegramSender instances..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# 2. Compile and publish in Release mode directly
# NOTE: Do NOT use Start-Process with -Wait here, as MSBuild build servers inherit redirected stdout/stderr pipes, causing CI/agent terminals to hang indefinitely.
# Always use direct execution with --disable-build-servers -m:1.
Write-Host "`n[*] Running dotnet publish (Release, win-x64)..." -ForegroundColor Green

& dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true --self-contained false --disable-build-servers -m:1

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[X] Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Verify output exe
$publishExe = Join-Path $scriptDir "bin\Release\net9.0-windows\win-x64\publish\GDriveTelegramSender.exe"

if (-not (Test-Path $publishExe)) {
    Write-Host "`n[X] Published executable not found at: $publishExe" -ForegroundColor Red
    exit 1
}

$exeItem = Get-Item $publishExe
$sizeMb = [Math]::Round($exeItem.Length / 1MB, 2)

Write-Host "`n[+] Build successful!" -ForegroundColor Green
Write-Host "    Exe Path: $($exeItem.FullName)" -ForegroundColor Gray
Write-Host "    Size:     $sizeMb MB" -ForegroundColor Gray
Write-Host "    Updated:  $($exeItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray

# 4. Update SendTo shortcut
try {
    $sendToDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::SendTo)
    if (Test-Path $sendToDir) {
        $shortcutPath = Join-Path $sendToDir "Google Drive & Telegram.lnk"
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $publishExe
        $shortcut.WorkingDirectory = Split-Path -Parent $publishExe
        $shortcut.Description = "Google Drive and Telegram Sender"
        $shortcut.Save()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($wshShell) | Out-Null

        Write-Host "`n[+] SendTo context menu shortcut updated successfully!" -ForegroundColor Green
        Write-Host "    Shortcut: $shortcutPath" -ForegroundColor Gray
    }
}
catch {
    Write-Host "`n[!] Could not update SendTo shortcut: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n=================================================" -ForegroundColor Cyan
Write-Host "   Ready to use!                                 " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
