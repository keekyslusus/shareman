[CmdletBinding()]
param(
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$e = [char]27
$lavenderish = "$e[38;2;208;188;255m"
$reset = "$e[0m"

function Wait-BeforeClosing {
    if ($NoPause) { return }
    if (-not [Environment]::UserInteractive) { return }
    if ([Console]::IsInputRedirected) { return }

    Write-Host ''
    Write-Host "${lavenderish}Done. " -NoNewline
    Write-Host 'Press any key to close this window...'

    try {
        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    }
    catch {
        $null = Read-Host 'Press Enter to close'
    }
}

$exitCode = 0
Push-Location -LiteralPath $PSScriptRoot
try {
    $workspace = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')

    $processes = Get-Process -Name 'shareman', 'GDriveTelegramSender' -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host 'Stopping running shareman instances...'
        $processes | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }

    $outputDirectory = Join-Path $workspace 'bin\Release\net9.0-windows\win-x64'

    Write-Host 'Running dotnet publish (Release, win-x64, single-file, ReadyToRun)...'
    & dotnet publish (Join-Path $workspace 'shareman.csproj') -c Release -r win-x64 /p:PublishSingleFile=true /p:PublishReadyToRun=true --self-contained false --disable-build-servers -m:1 -o $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publish of shareman failed with exit code $LASTEXITCODE." }

    $publishSubfolder = Join-Path $outputDirectory 'publish'
    if (Test-Path -LiteralPath $publishSubfolder) {
        Remove-Item -LiteralPath $publishSubfolder -Recurse -Force -ErrorAction SilentlyContinue
    }

    $publishExe = Join-Path $outputDirectory 'shareman.exe'
    if (-not (Test-Path -LiteralPath $publishExe -PathType Leaf)) { throw "Published executable not found: $publishExe" }

    $legacyPublishExe = Join-Path $outputDirectory 'GDriveTelegramSender.exe'
    if (Test-Path -LiteralPath $legacyPublishExe) {
        Remove-Item -LiteralPath $legacyPublishExe -Force -ErrorAction SilentlyContinue
    }

    $releaseFiles = @('README.md', 'LICENSE')
    foreach ($file in $releaseFiles) {
        $sourcePath = Join-Path $workspace $file
        if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $outputDirectory $file) -Force
        }
    }

    [xml]$projXml = Get-Content (Join-Path $workspace 'shareman.csproj')
    $appVersion = $projXml.Project.PropertyGroup.Version
    if (-not $appVersion) { $appVersion = '1.0.0' }

    $stagingDir = Join-Path $outputDirectory 'staging_zip'
    $stagingAppDir = Join-Path $stagingDir 'shareman'
    if (Test-Path -LiteralPath $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $stagingAppDir -Force | Out-Null

    Get-ChildItem -LiteralPath $outputDirectory | Where-Object {
        $_.Name -ne 'staging_zip' -and $_.Name -ne 'UserData' -and $_.Extension -ne '.zip'
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stagingAppDir -Recurse -Force
    }

    $zipName = "shareman-v${appVersion}-win-x64.zip"
    $zipPath = Join-Path $outputDirectory $zipName
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Write-Host "Creating release archive ($zipName)..."
    Compress-Archive -Path $stagingAppDir -DestinationPath $zipPath -Force

    Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue

    $exeItem = Get-Item -LiteralPath $publishExe
    $sizeMb = [Math]::Round($exeItem.Length / 1MB, 2)
    $relativePublish = Resolve-Path -Relative -LiteralPath $publishExe

    $zipItem = Get-Item -LiteralPath $zipPath
    $zipSizeMb = [Math]::Round($zipItem.Length / 1MB, 2)
    $relativeZip = Resolve-Path -Relative -LiteralPath $zipPath

    Write-Host ''
    Write-Host 'Published executable: ' -NoNewline
    Write-Host "${lavenderish}$relativePublish"
    Write-Host 'Size: ' -NoNewline
    Write-Host "${lavenderish}$sizeMb MB"
    Write-Host 'SHA256: ' -NoNewline
    Write-Host "${lavenderish}$((Get-FileHash -LiteralPath $publishExe -Algorithm SHA256).Hash)"

    Write-Host ''
    Write-Host 'Release package: ' -NoNewline
    Write-Host "${lavenderish}$relativeZip"
    Write-Host 'Size: ' -NoNewline
    Write-Host "${lavenderish}$zipSizeMb MB"
    Write-Host 'SHA256: ' -NoNewline
    Write-Host "${lavenderish}$((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
}
catch {
    $exitCode = 1
    Write-Host ''
    Write-Host 'RELEASE FAILED' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
finally {
    Pop-Location
    Wait-BeforeClosing
}

exit $exitCode
