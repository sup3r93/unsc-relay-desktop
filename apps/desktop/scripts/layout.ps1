param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$Thumbprint, [switch]$AllowUntrusted)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskPublish = Join-Path $taskRepo 'artifacts\publish'
$taskDist = Join-Path $taskRepo 'artifacts\distribution'
$taskLibrary = Join-Path $taskDist 'Librerie'
$taskNative = Join-Path $taskRepo 'artifacts\native'
$taskRetiredManifest = Join-Path $PSScriptRoot 'retired-support-files.txt'
$taskRetired = @()
if (Test-Path -LiteralPath $taskRetiredManifest) { $taskRetired = @(Get-Content -LiteralPath $taskRetiredManifest | Where-Object { $_.Length -gt 0 }) }
$taskCurrentFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -LiteralPath $taskPublish -File -Recurse | ForEach-Object { [void]$taskCurrentFiles.Add($_.FullName.Substring($taskPublish.Length + 1)) }
if (Test-Path -LiteralPath $taskLibrary) {
    Get-ChildItem -LiteralPath $taskLibrary -File -Recurse | Where-Object { $_.Name -notlike '*smoke-result.json' -and $_.Name -ne 'presentation-confirmation.png' -and $_.FullName -notmatch '\\\.(smoke-data|config-log-fixtures)\\' } | ForEach-Object {
        $taskRelative = $_.FullName.Substring($taskLibrary.Length + 1)
        if (!$taskCurrentFiles.Contains($taskRelative)) { $taskRetired += $taskRelative }
    }
}
$taskRetired = @($taskRetired | Sort-Object -Unique | Where-Object { !$taskCurrentFiles.Contains($_) -and $_ -notmatch '^Tools\\Cygwin\\(tmp|home|var\\log)\\' })
foreach ($taskRetiredFile in $taskRetired) {
    if ([IO.Path]::IsPathRooted($taskRetiredFile) -or $taskRetiredFile.Split('\') -contains '..' -or $taskRetiredFile.Contains('"')) { throw 'Elenco file dismessi non valido.' }
}
Set-Content -LiteralPath $taskRetiredManifest -Value $taskRetired -Encoding UTF8
if (Test-Path -LiteralPath $taskDist) {
    $taskResolvedDist = (Resolve-Path -LiteralPath $taskDist).Path
    $taskExpectedDist = [IO.Path]::GetFullPath((Join-Path $taskRepo 'artifacts\distribution'))
    if ($taskResolvedDist -ne $taskExpectedDist -or (Get-Item -LiteralPath $taskDist).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Percorso di preparazione pacchetto inatteso.' }
    Remove-Item -LiteralPath $taskResolvedDist -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $taskLibrary,$taskNative | Out-Null
# Copy only a freshly published application. Diagnostics and test data never enter the distribution.
Get-ChildItem -LiteralPath $taskPublish -Force | Where-Object { $_.Name -notin '.smoke-data','.config-log-fixtures','smoke-result.json','presentation-confirmation.png' -and $_.Extension -ne '.pdb' } |
    Copy-Item -Destination $taskLibrary -Recurse -Force
# Satellite resources have a dedicated root folder; runtime resolves them through LanguageResources.
$taskLanguages = Join-Path $taskDist 'languages'
New-Item -ItemType Directory -Force -Path $taskLanguages | Out-Null
Get-ChildItem -LiteralPath $taskLibrary -Directory | Where-Object { $_.Name -match '^[a-z]{2}(-[A-Za-z]{2,8})?$' -and @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.resources.dll' -File).Count -gt 0 } | ForEach-Object {
    $taskLocale = $_.FullName
    $taskTargetLocale = Join-Path $taskLanguages $_.Name
    New-Item -ItemType Directory -Force -Path $taskTargetLocale | Out-Null
    Get-ChildItem -LiteralPath $taskLocale -File -Filter '*.resources.dll' | Move-Item -Destination $taskTargetLocale
    if (@(Get-ChildItem -LiteralPath $taskLocale -Force).Count -eq 0) { Remove-Item -LiteralPath $taskLocale }
}
$taskVsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$taskCompiler = & $taskVsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe' | Select-Object -First 1
if (!$taskCompiler) { throw 'Compilatore C++ x64 non disponibile per il launcher nativo.' }
$taskVcRoot = Split-Path (Split-Path (Split-Path (Split-Path $taskCompiler -Parent) -Parent) -Parent) -Parent
$taskSdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$taskSdkVersion = Get-ChildItem -LiteralPath (Join-Path $taskSdkRoot 'Include') -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'um\Windows.h') } | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty Name
if (!$taskSdkVersion) { throw 'Windows SDK non disponibile.' }
$taskIncludes = @((Join-Path $taskVcRoot 'include')) + @('um','shared','ucrt') | ForEach-Object { if ($_ -in 'um','shared','ucrt') { Join-Path $taskSdkRoot "Include\$taskSdkVersion\$_" } else { $_ } }
$taskRc = Join-Path $taskSdkRoot "bin\$taskSdkVersion\x64\rc.exe"
$taskIcon = (Join-Path $taskRepo 'src\UnscRelay.App\Assets\Relay.ico').Replace('\','\\')
$taskVersion = [version]$Version
$taskNumbers = "$($taskVersion.Major),$($taskVersion.Minor),$($taskVersion.Build),0"
$taskResource = @"
1 ICON "$taskIcon"
1 VERSIONINFO
FILEVERSION $taskNumbers
PRODUCTVERSION $taskNumbers
FILEOS 0x40004
FILETYPE 1
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904b0"
  BEGIN
   VALUE "CompanyName", "Daniele / UNSC Relay\0"
   VALUE "FileDescription", "UNSC Relay\0"
   VALUE "ProductName", "UNSC Relay\0"
   VALUE "FileVersion", "$Version\0"
   VALUE "ProductVersion", "$Version\0"
   VALUE "OriginalFilename", "UNSC Relay.exe\0"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@
$taskRcFile = Join-Path $taskNative 'Launcher.rc'
Set-Content -LiteralPath $taskRcFile -Value $taskResource -Encoding Unicode
& $taskRc /nologo /fo (Join-Path $taskNative 'Launcher.res') $taskRcFile
if ($LASTEXITCODE -ne 0) { throw 'Creazione risorse launcher fallita.' }
$taskCompilerArgs = @('/nologo','/utf-8','/std:c++17','/guard:cf','/O2','/MT','/EHsc','/DUNICODE','/D_UNICODE') + ($taskIncludes | ForEach-Object { '/I' + $_ }) + @(
    (Join-Path $taskRepo 'native\Launcher.cpp'), ('/Fo' + (Join-Path $taskNative 'Launcher.obj')), ('/Fe' + (Join-Path $taskDist 'UNSC Relay.exe')),
    (Join-Path $taskNative 'Launcher.res'), '/link','/SUBSYSTEM:WINDOWS','/DYNAMICBASE','/NXCOMPAT','/GUARD:CF','/HIGHENTROPYVA',
    ('/LIBPATH:' + (Join-Path $taskVcRoot 'lib\x64')), ('/LIBPATH:' + (Join-Path $taskSdkRoot "Lib\$taskSdkVersion\um\x64")),
    ('/LIBPATH:' + (Join-Path $taskSdkRoot "Lib\$taskSdkVersion\ucrt\x64")), 'user32.lib')
& $taskCompiler @taskCompilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilazione launcher nativo fallita.' }
& "$PSScriptRoot\sign.ps1" -Path (Join-Path $taskDist 'UNSC Relay.exe') -Thumbprint $Thumbprint -AllowUntrusted:$AllowUntrusted
# Remove only exact files belonging to older flat packages. No wildcard or recursive deletion of user folders.
$taskLegacyLines = Get-ChildItem -LiteralPath $taskPublish -File -Recurse | Where-Object { $_.Name -notin 'UNSC Relay.exe','smoke-result.json' -and $_.FullName -notmatch '\\\.(smoke-data|config-log-fixtures)\\' } | ForEach-Object {
    $taskRelative = $_.FullName.Substring($taskPublish.Length + 1)
    'Type: files; Name: "{app}\' + $taskRelative + '"'
}
Set-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Value $taskLegacyLines -Encoding UTF8
Get-ChildItem -LiteralPath $taskPublish -File -Recurse -Filter '*.resources.dll' | ForEach-Object {
    $taskRelative = $_.FullName.Substring($taskPublish.Length + 1)
    if($taskRelative -match '^[a-z]{2}(-[A-Za-z]{2,8})?\\[^\\]+\.resources\.dll$') {
        Add-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Encoding UTF8 -Value ('Type: files; Name: "{app}\Librerie\' + $taskRelative + '"')
    }
}

foreach ($taskRetiredFile in $taskRetired) {
    Add-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Encoding UTF8 -Value ('Type: files; Name: "{app}\Librerie\' + $taskRetiredFile + '"')
    Add-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Encoding UTF8 -Value ('Type: files; Name: "{app}\' + $taskRetiredFile + '"')
}

# Prune only empty legacy directories, deepest first, after deleting their exact application files.
$taskLegacyDirs = @(Get-ChildItem -LiteralPath $taskPublish -Directory -Recurse | ForEach-Object { $_.FullName.Substring($taskPublish.Length + 1) })
$taskLegacyDirs += @($taskRetired | ForEach-Object { $taskParent=Split-Path $_ -Parent; while($taskParent){$taskParent;$taskParent=Split-Path $taskParent -Parent} })
foreach($taskDirectory in ($taskLegacyDirs | Sort-Object -Unique | Sort-Object { $_.Length } -Descending)) {
    if([IO.Path]::IsPathRooted($taskDirectory) -or $taskDirectory.Split('\') -contains '..') { throw 'Cartella legacy inattesa.' }
    Add-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Encoding UTF8 -Value ('Type: dirifempty; Name: "{app}\' + $taskDirectory + '"')
    if($taskDirectory -match '^[a-z]{2}(-[A-Za-z]{2,8})?$') { Add-Content -LiteralPath (Join-Path $taskNative 'legacy-files.iss') -Encoding UTF8 -Value ('Type: dirifempty; Name: "{app}\Librerie\' + $taskDirectory + '"') }
}
