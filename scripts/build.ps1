param([string]$Version='0.2.19', [Parameter(Mandatory)][string]$Thumbprint, [switch]$AllowUntrusted, [switch]$PackageOnly)
$ErrorActionPreference='Stop'
$taskDesktopBuild=Join-Path (Split-Path $PSScriptRoot -Parent) 'apps\desktop\scripts\build.ps1'
& $taskDesktopBuild @PSBoundParameters
