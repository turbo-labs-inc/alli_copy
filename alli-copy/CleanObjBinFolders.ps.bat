# 2>NUL & @CLS & PUSHD "%~dp0" & "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -nol -nop -ep bypass "$PSScriptRoot = '%~f0';[IO.File]::ReadAllText('%~f0')|iex" & POPD & EXIT /B
$PSScriptRoot = $PWD #Need this because running as a bat causes us to lose $PSScriptRoot

cd $PSScriptRoot
gci -inc bin,obj -rec | rm -rec -force
pause