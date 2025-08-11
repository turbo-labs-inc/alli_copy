# 2>NUL & @CLS & PUSHD "%~dp0" & "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -nol -nop -ep bypass "$PSScriptRoot = '%~f0';[IO.File]::ReadAllText('%~f0')|iex" & POPD & EXIT /B
$PSScriptRoot = $PWD #Need this because running as a bat causes us to lose $PSScriptRoot

if( (gci -Filter *.sln).Length -ne 0){
    $AssemblyInfo = gci -Filter AssemblyInfo.cs -Recurse 
    $SearchString = '[assembly: AssemblyVersion("1.0.*")]'
    $ReplacementString = '[assembly: AssemblyVersion("1.0.0.0")]'

    $AssemblyInfo | ForEach-Object {
        $filepath = $_.FullName  

        $content = Get-Content $_.FullName 

        $line = $content | Select-String $SearchString -SimpleMatch | Select-Object -ExpandProperty Line | Where-Object{$_.length -ne 0 -and ($_.StartsWith("//") -ne $true)}


        if($line.length -gt 0){
            Write-Output "Updating: $filepath, Replacing: $line "
            $content | ForEach-Object {$_.Replace($line,$ReplacementString)} | Set-Content $filepath
        }
    }
} else {
    Write-Output "This command must be run in a top level sln directory"
}

pause

