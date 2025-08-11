#Requires -RunAsAdministrator
Import-Module Gravitate-Scripts *> $null # supress warnings from our custom function names

$ClientName = "ngl-wholesale"
$SolutionDirectory = $($PSScriptRoot).ToString();
Gravitate-IIS-Install-Client-API -ClientName $ClientName -SolutionDirectory $SolutionDirectory