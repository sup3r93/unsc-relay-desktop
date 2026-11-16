param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$Thumbprint,
    [string]$TimestampServer = '',
    [switch]$AllowUntrusted
)
$ErrorActionPreference = 'Stop'
$certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object Thumbprint -EQ $Thumbprint | Select-Object -First 1
if (!$certificate) { $certificate = Get-ChildItem Cert:\LocalMachine\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object Thumbprint -EQ $Thumbprint | Select-Object -First 1 }
if (!$certificate -or !$certificate.HasPrivateKey) { throw 'Certificato con chiave privata non disponibile.' }
if ($certificate.NotAfter -lt (Get-Date)) { throw 'Certificato scaduto.' }
if (!($certificate.EnhancedKeyUsageList | Where-Object ObjectId -EQ '1.3.6.1.5.5.7.3.3')) { throw 'Il certificato non consente la firma del codice.' }
$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$parameters = @{ LiteralPath = $resolvedPath; Certificate = $certificate; HashAlgorithm = 'SHA256'; IncludeChain = 'NotRoot' }
if ($TimestampServer) { $parameters.TimestampServer = $TimestampServer }
$signature = Set-AuthenticodeSignature @parameters
$signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
if ($signature.SignerCertificate.Thumbprint -ne $Thumbprint) { throw 'Firma assente o firmatario inatteso.' }
if ($signature.Status -ne 'Valid') {
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
        $null = $chain.Build($signature.SignerCertificate)
        $onlyUntrustedRoot = $chain.ChainStatus.Count -gt 0 -and @($chain.ChainStatus | Where-Object { $_.Status -ne [System.Security.Cryptography.X509Certificates.X509ChainStatusFlags]::UntrustedRoot }).Count -eq 0
        # Windows PowerShell and PowerShell 7 expose an untrusted self-signed root differently.
        if (!$AllowUntrusted -or $signature.Status -notin @('NotTrusted', 'UnknownError') -or !$onlyUntrustedRoot) { throw "Firma non valida: $($signature.Status) - $($signature.StatusMessage)" }
    } finally { $chain.Dispose() }
    Write-Warning 'Firma presente, ma certificato non riconosciuto come attendibile da Windows. Nessuna impostazione di attendibilita viene modificata.'
}
Write-Output "Firmato: $resolvedPath ($($signature.Status))"
