# 2>NUL & @CLS & PUSHD "%~dp0" & "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -nol -nop -ep bypass "'$PSScriptRoot = ""%~dp0"";'+[IO.File]::ReadAllText('%~f0')|iex" & POPD & EXIT /B

cd $PSScriptRoot
echo "Deleting Obj & Bin Folders"
gci -inc bin,obj -rec | foreach{Write-Host $_.fullName;}
gci -inc bin,obj -rec | rm -rec -force
echo "Attempting to Delete Contents of Packages"
gci -dir -rec -inc packages | gci -dir | foreach{Write-Host $_.fullName;}
gci -dir -rec -inc packages | gci -dir | ForEach-Object {
    try {
        Remove-Item -Path $_.FullName -Force -Recurse -ErrorAction SilentlyContinue
    }
    catch { echo "Could Not Remove $_.Name" }
}
pause