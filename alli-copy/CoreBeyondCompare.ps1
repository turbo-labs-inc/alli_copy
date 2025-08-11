#This Script Requires module Gravitate-Scripts, found in Gravitate.Scripts Repository
Import-Module Gravitate-Scripts -Force *> $null # supress warnings from our custom function names
$CoreProjectDirectory = "C:\workspace\gravitate\Gravitate.Core.CTRM_V5"
$ClientProjectDirectory = $PSScriptRoot
Gravitate-Utility-Beyond-Compare-Client-To-Core $ClientProjectDirectory $CoreProjectDirectory




