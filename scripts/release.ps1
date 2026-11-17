param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$NotesPath,
 [ValidateSet('interface','features','security')][string[]]$Categories=@('features'),
 [ValidateSet('normal','important','critical')][string]$SecurityLevel='normal',
 [ValidatePattern('^\d+\.\d+\.\d+$')][string]$MinimumSupportedVersion='0.0.0')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskNotes=if([IO.Path]::IsPathRooted($NotesPath)){$NotesPath}else{Join-Path $taskRoot $NotesPath}
$taskDesktopRelease=Join-Path $taskRoot 'apps\desktop\scripts\release.ps1'
& $taskDesktopRelease -Version $Version -NotesPath ([IO.Path]::GetFullPath($taskNotes)) -Categories $Categories -SecurityLevel $SecurityLevel -MinimumSupportedVersion $MinimumSupportedVersion
