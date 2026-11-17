param(
 [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
 [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$InstallerSha256,
 [Parameter(Mandatory)][string]$PublisherCertificate,
 [Parameter(Mandatory)][string]$OutputPath,
 [ValidateSet('interface','features','security')][string[]]$Categories=@('features'),
 [ValidateSet('normal','important','critical')][string]$SecurityLevel='normal',
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$MinimumSupportedVersion='0.0.0'
)
$ErrorActionPreference='Stop'
if(([version]$MinimumSupportedVersion) -gt ([version]$Version)){throw 'Minimum supported version exceeds release.'}
if($SecurityLevel -ne 'normal' -and 'security' -notin $Categories){throw 'Security updates require the security category.'}
if($SecurityLevel -eq 'critical' -and $MinimumSupportedVersion -eq '0.0.0'){throw 'Critical updates require an explicit minimum supported version.'}
if(@($Categories | Select-Object -Unique).Count -ne $Categories.Count){throw 'Duplicate categories.'}
$policyPublisher=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile((Resolve-Path -LiteralPath $PublisherCertificate).Path)
try {
 $policyCertificate=Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object Thumbprint -EQ $policyPublisher.Thumbprint | Select-Object -First 1
 if(!$policyCertificate){$policyCertificate=Get-ChildItem Cert:\LocalMachine\My -CodeSigningCert | Where-Object Thumbprint -EQ $policyPublisher.Thumbprint | Select-Object -First 1}
 if(!$policyCertificate -or !$policyCertificate.HasPrivateKey){throw 'Pinned signing certificate is unavailable.'}
 if((Get-Date) -lt $policyPublisher.NotBefore -or (Get-Date) -gt $policyPublisher.NotAfter){throw 'Signing certificate is outside its validity period.'}
 $policyPayload=[Text.Encoding]::UTF8.GetBytes((@{version=$Version;installerSha256=$InstallerSha256.ToLowerInvariant();categories=@($Categories);security=$SecurityLevel;minimumSupportedVersion=$MinimumSupportedVersion}|ConvertTo-Json -Compress))
 $policyKey=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($policyCertificate)
 if(!$policyKey){throw 'RSA signing key required.'}
 try {$policySignature=$policyKey.SignData($policyPayload,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)}finally{$policyKey.Dispose()}
 $policyBundle=@{payload=[Convert]::ToBase64String($policyPayload);signature=[Convert]::ToBase64String($policySignature)}|ConvertTo-Json -Compress
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$policyBundle,[Text.UTF8Encoding]::new($false))
} finally {$policyPublisher.Dispose()}
