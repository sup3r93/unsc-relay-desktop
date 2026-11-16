param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$NotesPath)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskNotes=if([IO.Path]::IsPathRooted($NotesPath)){$NotesPath}else{Join-Path $taskRoot $NotesPath}
$taskDesktopRelease=Join-Path $taskRoot 'apps\desktop\scripts\release.ps1'
& $taskDesktopRelease -Version $Version -NotesPath ([IO.Path]::GetFullPath($taskNotes))
