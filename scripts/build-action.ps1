# PowerShell 7, available on GitHub-hosted Windows, macOS and Linux runners.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Config,
    [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64','osx-x64','osx-arm64','linux-x64','linux-arm64')][string]$Runtime,
    [string]$Output = 'installer-output',
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$')][string]$SdkVersion = 'v0.2.0'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$hostOs = if ($IsWindows) { 'win' } elseif ($IsMacOS) { 'osx' } elseif ($IsLinux) { 'linux' } else { throw 'Unsupported host OS.' }
$hostArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$hostRuntime = "$hostOs-$hostArch"
if ($Runtime -ne $hostRuntime) { throw "Runner is $hostRuntime; use a matching runner for $Runtime. For cross-packaging use divinepack build --runtime-dir directly." }
if (-not $env:RUNNER_TEMP) { throw 'RUNNER_TEMP is required; run this helper in GitHub Actions.' }
$configPath = (Resolve-Path -LiteralPath $Config).Path
$outputPath = [IO.Path]::GetFullPath($Output)
if ((Test-Path -LiteralPath $outputPath) -and @(Get-ChildItem -LiteralPath $outputPath -Force).Count -gt 0) { throw 'Choose a new, empty output directory.' }
$work = Join-Path $env:RUNNER_TEMP ('divinelabs-sdk-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$extension = if ($IsWindows) { 'zip' } else { 'tar.gz' }
$asset = "DivineLabs-Installer-$Runtime.$extension"
$release = "https://github.com/TheDivineLabs/DivineLabs_Installer/releases/download/$SdkVersion"
$download = Join-Path $work $asset
Invoke-WebRequest -Uri "$release/$asset" -OutFile $download
$checksumPath = Join-Path $work 'SHA256SUMS.txt'
Invoke-WebRequest -Uri "$release/SHA256SUMS.txt" -OutFile $checksumPath
$checksums = Get-Content -LiteralPath $checksumPath -Raw
$expected = @([string]$checksums -split '\r?\n' | Where-Object { $_ -match ('^[a-fA-F0-9]{64}  ' + [regex]::Escape($asset) + '$') })
if ($expected.Count -ne 1) { throw "Release checksum missing or duplicated: $asset" }
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expected[0].Substring(0,64)) { throw "SDK checksum mismatch: $asset" }
$sdk = Join-Path $work 'sdk'; New-Item -ItemType Directory -Path $sdk | Out-Null
if ($IsWindows) { Expand-Archive -LiteralPath $download -DestinationPath $sdk }
else { & tar -xzf $download -C $sdk; if ($LASTEXITCODE -ne 0) { throw 'Cannot extract SDK.' } }
$cli = Join-Path $sdk $(if ($IsWindows) { 'divinepack.exe' } else { 'divinepack' })
& $cli validate --config $configPath
if ($LASTEXITCODE -ne 0) { throw 'Installer config validation failed.' }
& $cli build --config $configPath --runtime $Runtime --output $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$archives = @(Get-ChildItem -LiteralPath $outputPath -File | Where-Object { $_.Name.EndsWith('.zip') -or $_.Name.EndsWith('.tar.gz') })
if ($archives.Count -ne 1) { throw 'Expected exactly one installer archive.' }
$archive = $archives[0].FullName
if ($env:GITHUB_OUTPUT) {
    $delimiter = [Guid]::NewGuid().ToString('N')
    "archive<<$delimiter`n$archive`n$delimiter" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}
Write-Host "Installer archive: $archive"
# RUNNER_TEMP is runner-managed; no recursive deletion of externally supplied paths.
