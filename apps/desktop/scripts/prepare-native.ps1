param([Parameter(Mandatory)][string]$Thumbprint,[switch]$AllowUntrusted)
$ErrorActionPreference='Stop'
$taskRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'src\UnscRelay.App\Tools'
$taskManifest=Join-Path $taskRoot 'bundled-manifest.json'
if(!(Test-Path -LiteralPath $taskManifest)){throw 'Eseguire prima scripts/fetch-native.py per preparare i componenti inclusi.'}
$taskHashes=Get-Content -LiteralPath $taskManifest -Raw|ConvertFrom-Json -AsHashtable
foreach($taskEntry in $taskHashes.GetEnumerator()){
 $taskPath=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskEntry.Key))
 if(!$taskPath.StartsWith([IO.Path]::GetFullPath($taskRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Percorso componente inatteso.'}
 if((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskEntry.Value){throw ('Componente alterato: '+$taskEntry.Key)}
}
foreach($taskFile in (Get-ChildItem -LiteralPath $taskRoot -File -Recurse -Filter '*.exe')){
 $taskSignature=Get-AuthenticodeSignature -LiteralPath $taskFile.FullName
 if($taskSignature.Status -eq 'Valid' -or $taskSignature.SignerCertificate.Thumbprint -eq $Thumbprint){continue}
 & "$PSScriptRoot\sign.ps1" -Path $taskFile.FullName -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
}
$taskUpdated=[ordered]@{}
foreach($taskEntry in $taskHashes.GetEnumerator()|Sort-Object Key){$taskUpdated[$taskEntry.Key]=(Get-FileHash -LiteralPath (Join-Path $taskRoot $taskEntry.Key) -Algorithm SHA256).Hash.ToLowerInvariant()}
$taskUpdated|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $taskManifest -Encoding utf8
Write-Output 'Componenti verificati e EXE firmati; firme ufficiali valide preservate.'
