param([Parameter(Mandatory)][string]$Thumbprint)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskFixture=Join-Path $taskRepo ('artifacts\legacy-cleanup-'+[Guid]::NewGuid().ToString('N'))
$taskPublish=Join-Path $taskFixture 'publish'
$taskNative=Join-Path $taskFixture 'native'
$taskInstalled=Join-Path $taskFixture 'installed'
New-Item -ItemType Directory -Path $taskPublish,$taskNative,$taskInstalled -Force | Out-Null
$taskLocales=@('cs','de','es','fr','it','ko','pl','ru','tr','zh-Hans','zh-Hant')
foreach($taskName in $taskLocales){
 foreach($taskBase in @($taskPublish,$taskInstalled)){
  $taskFolder=Join-Path $taskBase $taskName
  New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
  Set-Content -LiteralPath (Join-Path $taskFolder 'Fixture.resources.dll') -Value 'known old resource'
 }
}
foreach($taskBase in @($taskPublish,$taskInstalled)){
 New-Item -ItemType Directory -Path (Join-Path $taskBase 'Web\media\a'),(Join-Path $taskBase 'Web\vendor\a'),(Join-Path $taskBase 'runtimes\win-x64\native') -Force | Out-Null
}
Set-Content -LiteralPath (Join-Path $taskInstalled 'cs\user-file.txt') -Value 'preserve custom file'
$taskRetired=@()
$taskLayout=Get-Content -LiteralPath (Join-Path $taskRepo 'scripts\layout.ps1') -Raw
$taskMarker='# Remove only exact files belonging to older flat packages.'
$taskCleanup=$taskLayout.Substring($taskLayout.IndexOf($taskMarker,[StringComparison]::Ordinal))
# Exercise the actual production directive generator and Inno's deletion behavior.
& ([scriptblock]::Create($taskCleanup))
$taskSpec=@"
[Setup]
AppId=UNSC-Relay-Cleanup-Regression
AppName=UNSC Relay cleanup regression
AppVersion=1
DefaultDirName=$taskInstalled
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
DisableWelcomePage=yes
DisableFinishedPage=yes
OutputDir=$taskFixture
OutputBaseFilename=cleanup-test
[InstallDelete]
#include "$taskNative\legacy-files.iss"
"@
$taskIss=Join-Path $taskFixture 'test.iss'
Set-Content -LiteralPath $taskIss -Value $taskSpec -Encoding utf8
$taskCompiler=@("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe","${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
& $taskCompiler /Qp $taskIss *> (Join-Path $taskFixture 'compile.log')
if($LASTEXITCODE -ne 0){throw "Fixture compiler failed: $taskFixture"}
$taskInstaller=Join-Path $taskFixture 'cleanup-test.exe'
& (Join-Path $taskRepo 'scripts\sign.ps1') -Path $taskInstaller -Thumbprint $Thumbprint -AllowUntrusted
$taskRun=Start-Process -FilePath $taskInstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -PassThru -WindowStyle Hidden
if(!$taskRun.WaitForExit(30000) -or $taskRun.ExitCode -ne 0){throw 'Fixture installation failed'}
foreach($taskName in $taskLocales | Where-Object {$_ -ne 'cs'}){if(Test-Path -LiteralPath (Join-Path $taskInstalled $taskName)){throw "Empty legacy directory retained: $taskName"}}
foreach($taskName in @('Web','runtimes')){if(Test-Path -LiteralPath (Join-Path $taskInstalled $taskName)){throw "Empty legacy subtree retained: $taskName"}}
if(!(Test-Path -LiteralPath (Join-Path $taskInstalled 'cs\user-file.txt')) -or (Test-Path -LiteralPath (Join-Path $taskInstalled 'cs\Fixture.resources.dll'))){throw 'Known resource/custom file preservation mismatch'}
Write-Output 'PASS: all equal-length locale directories removed; nested empty trees removed; custom file preserved; known resource removed.'
