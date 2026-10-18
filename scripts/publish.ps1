param(
 [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64','osx-x64','osx-arm64','linux-x64','linux-arm64')][string]$Runtime,
 [string]$Output = '',
 [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $root "artifacts/sdk-$Runtime" }
$Output = [IO.Path]::GetFullPath($Output)
if ((Test-Path -LiteralPath $Output) -and (Get-ChildItem -LiteralPath $Output -Force)) { throw 'Choose a new empty SDK output directory.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$runtimeOutput = Join-Path $Output 'runtime'
& $Dotnet publish "$root/src/DivineLabs.Installer.Desktop" -c Release -r $Runtime --self-contained true -p:PublishAot=true -p:IlcOptimizationPreference=Size -p:StripSymbols=true -p:DebugType=None -o $runtimeOutput
if ($LASTEXITCODE -ne 0) { throw 'Desktop Native AOT publish failed.' }
& $Dotnet publish "$root/src/DivineLabs.Installer.Cli" -c Release -r $Runtime --self-contained true -p:PublishAot=true -p:IlcOptimizationPreference=Size -p:StripSymbols=true -p:DebugType=None -o $Output
if ($LASTEXITCODE -ne 0) { throw 'CLI Native AOT publish failed.' }
$entryPoint = if ($Runtime.StartsWith('win-')) { 'DivineLabs.Installer.exe' } else { 'DivineLabs.Installer' }
@{formatVersion=1;runtime=$Runtime;entryPoint=$entryPoint;nativeAot=$true} | ConvertTo-Json | Set-Content -LiteralPath "$runtimeOutput/runtime.json" -Encoding utf8
Copy-Item -LiteralPath "$root/LICENSE","$root/NOTICE.md" -Destination $Output
Copy-Item -LiteralPath "$root/schemas" -Destination $Output -Recurse
Copy-Item -LiteralPath "$root/assets/licenses" -Destination $Output -Recurse
$commit = (& git -C $root rev-parse HEAD 2>$null)
@{version='0.1.0';runtime=$Runtime;nativeAot=$true;sourceCommit=$commit;builtAt=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath "$Output/build.json" -Encoding utf8
Get-ChildItem -LiteralPath $Output -File -Recurse | Sort-Object FullName | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($Output,$_.FullName).Replace('\','/')
 "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relative"
} | Set-Content -LiteralPath "$Output/SHA256SUMS.txt" -Encoding utf8
Write-Output "SDK: $Output"
