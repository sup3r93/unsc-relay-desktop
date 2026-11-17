param([string]$Version = '0.2.17', [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$Thumbprint, [switch]$AllowUntrusted, [switch]$PackageOnly)
$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path $PSScriptRoot -Parent
Push-Location $repositoryPath
try {
    $taskPuttygen=Join-Path $repositoryPath 'src\UnscRelay.App\Tools\PuTTYgen\puttygen.exe'
    if ((Get-FileHash $taskPuttygen -Algorithm SHA256).Hash -ne 'CAA8870591712C44DCC76A639AE6723C62167EAB6C4F1CB436B3A05D61741302') { throw 'Digest PuTTYgen inatteso.' }
    if ((Get-AuthenticodeSignature $taskPuttygen).Status -ne 'Valid') { throw 'Firma ufficiale PuTTYgen non valida.' }
    & "$PSScriptRoot\prepare-native.ps1" -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
    # Never execute unsigned development apphosts.
    dotnet build tests/UnscRelay.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Compilazione test fallita.' }
    & "$PSScriptRoot\sign.ps1" -Path tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
    if (!$PackageOnly) {
        & ./tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe
        if ($LASTEXITCODE -ne 0) { throw 'Test falliti.' }
        & ./tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe --cloud-sync-only
        if ($LASTEXITCODE -ne 0) { throw 'Test migrazione e sincronizzazione falliti.' }
        & ./tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe --native-certificates-only
        if ($LASTEXITCODE -ne 0) { throw 'Test certificati OpenSSH falliti.' }
    } else { Write-Warning 'Sola creazione pacchetto: i test non sono stati eseguiti. Artefatto non validato per il rilascio.' }
    $taskPublish = Join-Path $repositoryPath 'artifacts\publish'
    if (Test-Path -LiteralPath $taskPublish) {
        $taskResolved = (Resolve-Path -LiteralPath $taskPublish).Path
        if ($taskResolved -ne [IO.Path]::GetFullPath($taskPublish) -or (Get-Item -LiteralPath $taskPublish).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Percorso di pubblicazione inatteso.' }
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
    dotnet publish src/UnscRelay.App -c Release -r win-x64 --self-contained true -p:Version=$Version -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Compilazione fallita.' }
    & "$PSScriptRoot\sign.ps1" -Path 'artifacts/publish/UNSC Relay.exe' -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
    dotnet publish src/UnscRelay.Mcp -c Release -r win-x64 --self-contained true -p:Version=$Version -o artifacts/publish/ChatGPT
    if ($LASTEXITCODE -ne 0) { throw 'Compilazione bridge MCP fallita.' }
    & "$PSScriptRoot\sign.ps1" -Path 'artifacts/publish/ChatGPT/UNSC Relay MCP.exe' -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
    & "$PSScriptRoot\layout.ps1" -Version $Version -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
    $compilerPaths = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")
    $compilerPath = $compilerPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$compilerPath) { throw 'Inno Setup 6 non trovato.' }
    $shellPath = (Get-Process -Id $PID).Path
    $signCommand = '$q' + $shellPath + '$q -NoProfile -NonInteractive -File $q' + "$PSScriptRoot\sign.ps1" + '$q -Path $f -Thumbprint ' + $Thumbprint
    if ($AllowUntrusted) { $signCommand += ' -AllowUntrusted' }
    & $compilerPath /Qp "/DAppVersion=$Version" "/Srelay=$signCommand" scripts/installer.iss
    if ($LASTEXITCODE -ne 0) { throw 'Creazione installer fallita.' }
    $installerPath = Join-Path $repositoryPath "artifacts/installer/UNSC-Relay-Setup-$Version.exe"
    (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content "$installerPath.sha256"
    Write-Output $installerPath
} finally { Pop-Location }
