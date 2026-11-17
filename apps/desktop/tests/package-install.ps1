param([Parameter(Mandatory)][string]$Thumbprint,[string]$Version='0.2.17')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskFixture=Join-Path ([IO.Path]::GetTempPath()) ('ur-pkg-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$taskInstalled=Join-Path $taskFixture 'Chosen folder'
New-Item -ItemType Directory -Path $taskInstalled -Force | Out-Null
$taskIss=Join-Path $taskRoot ('scripts\.installer-verification-'+[Guid]::NewGuid().ToString('N')+'.iss')
$taskText=Get-Content -LiteralPath (Join-Path $taskRoot 'scripts\installer.iss') -Raw
# Exercise production files/cleanup/code while isolating all operating-system side effects.
$taskText=$taskText -replace '(?m)^AppId=.*$',('AppId=UNSC-Relay-Isolated-'+[Guid]::NewGuid().ToString('N'))
$taskText=$taskText -replace '(?m)^OutputDir=.*$',('OutputDir='+$taskFixture)
$taskText=$taskText -replace '(?m)^OutputBaseFilename=.*$','OutputBaseFilename=isolated-install-check'
$taskText=$taskText -replace '(?m)^SignedUninstaller=.*$','SignedUninstaller=no'
$taskText=$taskText -replace '(?m)^SignTool=.*$',''
$taskText=$taskText -replace '(?m)^CloseApplications=.*$','CloseApplications=no'
$taskText=$taskText -replace '(?m)^DefaultDirName=.*$',('DefaultDirName='+$taskInstalled)
$taskText=$taskText -replace '(?m)^DefaultGroupName=.*$',"DefaultGroupName=UNSC Relay isolated check`nCreateUninstallRegKey=no`nUninstallable=no"
$taskText=[regex]::Replace($taskText,'(?ms)^\[Icons\].*?(?=^\[Code\])','')
Set-Content -LiteralPath $taskIss -Value $taskText -Encoding utf8
try {
 $taskCompiler=@("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe","${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")|Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
 & $taskCompiler /Qp "/DAppVersion=$Version" $taskIss *> (Join-Path $taskFixture 'compile.log')
 if($LASTEXITCODE -ne 0){throw "Isolated installer compile failed: $taskFixture"}
 $taskExe=Join-Path $taskFixture 'isolated-install-check.exe'
 & (Join-Path $taskRoot 'scripts\sign.ps1') -Path $taskExe -Thumbprint $Thumbprint -AllowUntrusted
 function Install-Fixture {
  $taskRun=Start-Process -FilePath $taskExe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$taskInstalled+'"'),('/LOG="'+(Join-Path $taskFixture 'install.log')+'"') -WindowStyle Hidden -PassThru
  if(!$taskRun.WaitForExit(120000)){if(!$taskRun.HasExited){$taskRun.Kill()};throw 'Isolated installation timeout'}; $taskRun.Refresh(); if($taskRun.ExitCode -ne 0){throw ('Isolated installation failed: '+$taskRun.ExitCode+'; '+$taskFixture)}
 }
 $taskTooLong=Join-Path $taskFixture ('too-long-'+('x'*100))
 $taskRejected=Start-Process -FilePath $taskExe -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$taskTooLong+'"') -WindowStyle Hidden -PassThru
 if(!$taskRejected.WaitForExit(30000)){if(!$taskRejected.HasExited){$taskRejected.Kill()};throw 'Long path check timeout'}
 $taskRejected.Refresh();if($taskRejected.ExitCode -eq 0 -or (Test-Path -LiteralPath (Join-Path $taskTooLong 'UNSC Relay.exe'))){throw 'Long path guard failed'}
 Install-Fixture
 $taskApp=Join-Path $taskInstalled 'UNSC Relay.exe'
 if(!(Test-Path -LiteralPath $taskApp)-or !(Test-Path -LiteralPath (Join-Path $taskInstalled 'Librerie\UNSC Relay.exe'))){throw 'Installed package layout incomplete'}
 if((Get-AuthenticodeSignature -LiteralPath $taskApp).SignerCertificate.Thumbprint -ne $Thumbprint){throw 'Installed launcher signer differs'}
 $taskCustom=Join-Path $taskInstalled 'custom-user-file.txt';Set-Content -LiteralPath $taskCustom -Value 'keep this user file unchanged'
 $taskVault=Join-Path $taskFixture 'isolated-user-vault.bin';[IO.File]::WriteAllBytes($taskVault,[Security.Cryptography.RandomNumberGenerator]::GetBytes(1024))
 $taskCustomHash=(Get-FileHash -LiteralPath $taskCustom).Hash;$taskVaultHash=(Get-FileHash -LiteralPath $taskVault).Hash
 Install-Fixture
 if((Get-FileHash -LiteralPath $taskCustom).Hash -ne $taskCustomHash -or (Get-FileHash -LiteralPath $taskVault).Hash -ne $taskVaultHash){throw 'Upgrade changed isolated user data'}
 Write-Output 'PASS: signed fresh install in a chosen spaced path; cumulative reinstall preserves custom file and isolated vault; no app launched, personal registry, shortcuts or installed app touched.'
} finally {if(Test-Path -LiteralPath $taskIss){Remove-Item -LiteralPath $taskIss}}
