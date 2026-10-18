param([Parameter(Mandatory)][string]$Sdk,[string]$Work='')
$ErrorActionPreference='Stop'
$Sdk=(Resolve-Path -LiteralPath $Sdk).Path
if(-not $Work){$Work=Join-Path ([IO.Path]::GetTempPath()) ('DivineLabs-native-smoke-'+[Guid]::NewGuid().ToString('N'))}
$Work=[IO.Path]::GetFullPath($Work)
New-Item -ItemType Directory -Force $Work | Out-Null
$rid=([IO.File]::ReadAllText((Join-Path $Sdk 'runtime/runtime.json')) | ConvertFrom-Json).runtime
$debug=@(Get-ChildItem -LiteralPath $Sdk -Recurse -Force | Where-Object { $_.Extension -in '.pdb','.dbg','.dSYM' })
if($debug.Count){throw 'SDK contains unwanted debugging artifacts.'}
$extension=if($IsWindows){'.exe'}else{''}
$cli=Join-Path $Sdk "divinepack$extension"
function Run([string]$Executable,[string[]]$Arguments){
 $start=[Diagnostics.ProcessStartInfo]::new($Executable)
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($start)
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 if(-not $process.WaitForExit(180000)){throw "Native process timed out: $Executable"}
 $text=$stdout.GetAwaiter().GetResult();$errors=$stderr.GetAwaiter().GetResult()
 if($process.ExitCode -ne 0){throw "Native process failed ($($process.ExitCode)): $errors $text"}
 if($text){Write-Output $text.Trim()}
}
$sample=Join-Path $Work 'sample'
Run $cli @('init','--dir',$sample)
Run $cli @('validate','--config',"$sample/installer.json")
$built=Join-Path $Work 'built'
Run $cli @('build','--config',"$sample/installer.json",'--runtime',$rid,'--output',$built)
$runtimeName="DivineLabs.Installer$extension"
$gui=Get-ChildItem -LiteralPath $built -Recurse -File | Where-Object Name -eq $runtimeName | Select-Object -First 1
if(-not $gui){throw 'Native installer is missing.'}
if(@(Get-ChildItem -LiteralPath $built -Recurse -Force | Where-Object { $_.Extension -in '.pdb','.dbg','.dSYM' }).Count){throw 'Built installer contains debugging artifacts.'}
$target=Join-Path $Work 'application'
Run $gui.FullName @('--silent','--no-integrations','--dir',$target)
$receiptFile=Join-Path $target '.divinelabs-installer/receipt.json'
if(-not(Test-Path -LiteralPath $receiptFile)){throw 'Installer did not write receipt.'}
$receipt=Get-Content -LiteralPath $receiptFile -Raw | ConvertFrom-Json
if(-not(Test-Path -LiteralPath (Join-Path $target $receipt.entryPoint))){throw 'Installed application is missing.'}
New-Item -ItemType Directory -Force "$target/data" | Out-Null
[IO.File]::WriteAllText("$target/data/settings.json",'retained sentinel')
$uninstaller=Join-Path $target ".divinelabs-installer/runtime/$runtimeName"
Run $uninstaller @('--uninstall','--silent','--dir',$target)
$deadline=(Get-Date).AddSeconds(90)
while((Test-Path -LiteralPath $receiptFile)-and (Get-Date)-lt $deadline){Start-Sleep -Milliseconds 200}
if(Test-Path -LiteralPath $receiptFile){throw 'Installed native uninstaller did not complete.'}
if(Test-Path -LiteralPath (Join-Path $target $receipt.entryPoint)){throw 'Uninstaller left owned app executable.'}
if([IO.File]::ReadAllText("$target/data/settings.json") -ne 'retained sentinel'){throw 'Uninstaller lost user settings.'}
@{success=$true;runtime=$rid;nativeAot=$true;configBuild=$true;installedUninstaller=$true;settingsPreserved=$true;integrations=$false;applicationLaunched=$false} | ConvertTo-Json | Set-Content -LiteralPath "$Work/result.json" -Encoding utf8
Write-Output "Native smoke passed: $rid; $Work/result.json"
