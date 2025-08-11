Invoke-Command -Computername "GravitateDev" -ScriptBlock { Stop-Service 'GravitateQuartzServer$Gravitate_RaceTrac' }
