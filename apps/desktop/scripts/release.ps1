param([Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
      [Parameter(Mandatory)][string]$NotesPath)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
Push-Location $taskRepository
try {
    if ((git branch --show-current).Trim() -ne 'main' -or (git status --porcelain)) { throw 'Release consentita soltanto da main senza modifiche locali.' }
    $taskCommit = (git rev-parse HEAD).Trim()
    if ($taskCommit -ne (git rev-parse origin/main).Trim()) { throw 'main deve essere sincronizzata con origin/main.' }
    $taskInstaller = Join-Path $taskRepository "artifacts\installer\UNSC-Relay-Setup-$Version.exe"
    $taskExpected = (Get-Content -LiteralPath ($taskInstaller + '.sha256') -Raw).Trim()
    if ((Get-FileHash -LiteralPath $taskInstaller -Algorithm SHA256).Hash -ne $taskExpected) { throw 'Digest installer non valido.' }
    $taskPublisher = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile((Join-Path $taskRepository 'src\UnscRelay.App\Assets\Publisher.cer'))
    try {
        $taskSignature = Get-AuthenticodeSignature -LiteralPath $taskInstaller
        if ($taskSignature.SignerCertificate.Thumbprint -ne $taskPublisher.Thumbprint) { throw 'Firmatario installer inatteso.' }
        if ($taskSignature.Status -ne 'Valid') {
            $taskChain = [Security.Cryptography.X509Certificates.X509Chain]::new()
            try {
                $taskChain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
                $null = $taskChain.Build($taskSignature.SignerCertificate)
                $taskOnlyPersonalRoot = $taskChain.ChainStatus.Count -gt 0 -and @($taskChain.ChainStatus | Where-Object { $_.Status -ne [Security.Cryptography.X509Certificates.X509ChainStatusFlags]::UntrustedRoot }).Count -eq 0
                if ($taskSignature.Status -notin @('UnknownError', 'NotTrusted') -or !$taskOnlyPersonalRoot) { throw 'Firma installer non valida.' }
            } finally { $taskChain.Dispose() }
        }
        if ((Get-Date) -lt $taskPublisher.NotBefore -or (Get-Date) -gt $taskPublisher.NotAfter) { throw 'Certificato non valido alla data corrente.' }
    } finally { $taskPublisher.Dispose() }
    # Use the existing Git credential broker only for publishing; never store this credential in the app or in files.
    $taskCredentialLines = "protocol=https`nhost=github.com`npath=sup3r93/unsc-relay-desktop.git`n`n" | git credential fill
    if ($LASTEXITCODE -ne 0) { throw 'Autenticazione GitHub non disponibile.' }
    $taskCredential = @{}
    foreach ($taskLine in $taskCredentialLines) { $taskPair = $taskLine.Split('=', 2); if ($taskPair.Length -eq 2) { $taskCredential[$taskPair[0]] = $taskPair[1] } }
    $taskHeaders = @{ Authorization = 'Bearer ' + $taskCredential['password']; Accept = 'application/vnd.github+json'; 'User-Agent' = 'UNSC-Relay-Release' }
    $taskApi = 'https://api.github.com/repos/sup3r93/unsc-relay-desktop'
    $taskBody = @{ tag_name = "v$Version"; target_commitish = $taskCommit; name = "UNSC Relay $Version"; body = (Get-Content -LiteralPath $NotesPath -Raw); draft = $true; prerelease = $false } | ConvertTo-Json
    $taskRelease = Invoke-RestMethod -Uri "$taskApi/releases" -Headers $taskHeaders -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($taskBody))
    foreach ($taskFile in @($taskInstaller, ($taskInstaller + '.sha256'), (Join-Path $taskRepository 'artifacts\UNSC-Relay-Native-Sources.zip'))) {
        $taskName = [Uri]::EscapeDataString([IO.Path]::GetFileName($taskFile))
        $taskUpload = $taskRelease.upload_url.Split('{')[0] + '?name=' + $taskName
        $taskAsset = Invoke-RestMethod -Uri $taskUpload -Headers $taskHeaders -Method Post -ContentType 'application/octet-stream' -InFile $taskFile
        if ($taskFile -eq $taskInstaller -and $taskAsset.digest -ne ('sha256:' + $taskExpected.ToLowerInvariant())) { throw 'Digest GitHub diverso dal pacchetto locale. La release rimane in bozza.' }
    }
    $taskPublished = Invoke-RestMethod -Uri "$taskApi/releases/$($taskRelease.id)" -Headers $taskHeaders -Method Patch -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
    [pscustomobject]@{ Version = $taskPublished.tag_name; Url = $taskPublished.html_url; Commit = $taskCommit; Sha256 = $taskExpected }
} finally {
    if ($taskCredential) { $taskCredential.Clear() }; $taskCredentialLines = $null; $taskHeaders = $null
    Pop-Location
}
