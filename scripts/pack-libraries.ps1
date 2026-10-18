param([Parameter(Mandatory)][string]$Output, [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Output = [IO.Path]::GetFullPath($Output)
if ((Test-Path -LiteralPath $Output) -and @(Get-ChildItem -LiteralPath $Output -Force).Count -gt 0) { throw 'Choose a new, empty package output directory.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
foreach ($name in 'Core','Platforms','Components','Templates.Classic') {
    & $Dotnet pack "$root/src/DivineLabs.Installer.$name" -c Release -o $Output
    if ($LASTEXITCODE -ne 0) { throw "Cannot pack $name." }
}
Write-Output "Local NuGet feed: $Output"
