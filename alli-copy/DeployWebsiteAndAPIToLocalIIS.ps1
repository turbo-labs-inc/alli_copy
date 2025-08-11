#Requires -RunAsAdministrator
Import-Module Gravitate-Scripts *> $null # supress warnings from our custom function names
Set-Location $PSScriptRoot

$ClientName = "ngl-wholesale"
$SolutionDirectory = $($PSScriptRoot).ToString();
Gravitate-IIS-Install-Client-Website-And-API -ClientName $ClientName -SolutionDirectory $SolutionDirectory