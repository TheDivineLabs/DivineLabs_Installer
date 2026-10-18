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
& $Dotnet publish "$root/src/DivineLabs.Installer.Desktop" -c Release -r $Runtime --self-contained true -p:PublishAot=true -p:IlcOptimizationPreference=Size -p:StripSymbols=true -p:DebugType=None -p:NativeDebugSymbols=false -o $runtimeOutput
if ($LASTEXITCODE -ne 0) { throw 'Desktop Native AOT publish failed.' }
& $Dotnet publish "$root/src/DivineLabs.Installer.Cli" -c Release -r $Runtime --self-contained true -p:PublishAot=true -p:IlcOptimizationPreference=Size -p:StripSymbols=true -p:DebugType=None -p:NativeDebugSymbols=false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'CLI Native AOT publish failed.' }
# Remove only debug output inside the newly created SDK tree. Native dependency packages
# can ship PDBs even when the application's own debug information is disabled.
$prefix = $Output.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$entries = @(Get-ChildItem -LiteralPath $Output -Recurse -Force)
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'SDK output contains links; refusing debug-artifact cleanup.' }
$symbols = @($entries | Where-Object { (-not $_.PSIsContainer -and $_.Extension -in '.pdb','.dbg') -or ($_.PSIsContainer -and $_.Name.EndsWith('.dSYM',[StringComparison]::OrdinalIgnoreCase)) })
foreach ($item in $symbols | Sort-Object { $_.FullName.Length } -Descending) {
 if (-not ([IO.Path]::GetFullPath($item.FullName)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected debug-artifact path.' }
 if (Test-Path -LiteralPath $item.FullName) { Remove-Item -LiteralPath $item.FullName -Recurse -Force }
}
$entryPoint = if ($Runtime.StartsWith('win-')) { 'DivineLabs.Installer.exe' } else { 'DivineLabs.Installer' }
@{formatVersion=1;runtime=$Runtime;entryPoint=$entryPoint;nativeAot=$true;template='divinelabs-classic'} | ConvertTo-Json | Set-Content -LiteralPath "$runtimeOutput/runtime.json" -Encoding utf8
Copy-Item -LiteralPath "$root/LICENSE","$root/NOTICE.md" -Destination $Output
Copy-Item -LiteralPath "$root/schemas" -Destination $Output -Recurse
Copy-Item -LiteralPath "$root/assets/licenses" -Destination $Output -Recurse
$commit = (& git -C $root rev-parse HEAD 2>$null)
$version = ([xml](Get-Content -LiteralPath "$root/Directory.Build.props" -Raw)).Project.PropertyGroup.Version
@{version=$version;runtime=$Runtime;nativeAot=$true;sourceCommit=$commit;builtAt=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath "$Output/build.json" -Encoding utf8
Get-ChildItem -LiteralPath $Output -File -Recurse | Sort-Object FullName | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($Output,$_.FullName).Replace('\','/')
 "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relative"
} | Set-Content -LiteralPath "$Output/SHA256SUMS.txt" -Encoding utf8
Write-Output "SDK: $Output"
