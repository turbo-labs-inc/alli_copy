# 2>NUL & @CLS & PUSHD "%~dp0" & "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -nol -nop -ep bypass "$PSScriptRoot = '%~f0';[IO.File]::ReadAllText('%~f0')|iex" & POPD & EXIT /B
$PSScriptRoot = $PWD #Need this because running as a bat causes us to lose $PSScriptRoot

$NodeType = 'Deterministic'
$NodeValue = $true


if( (gci -Filter *.sln).Length -ne 0){
    gci -Filter *.csproj -Recurse | ForEach-Object {
        $fullName = $_.FullName
        [xml] $xml = Get-Content $fullName

        [System.Xml.XmlElement] $firstPropertyGroup = $xml.Project.PropertyGroup[0]
        #Remove Existing Nodes
        $firstPropertyGroup.SelectNodes('*') | Where-Object {$_.Name -eq 'Deterministic'} | ForEach-Object {
            [void]$firstPropertyGroup.RemoveChild($_)
        } 


        [System.Xml.XmlElement] $deterministic = $xml.CreateElement($NodeType,"http://schemas.microsoft.com/developer/msbuild/2003")
        $deterministic.InnerText = $NodeValue
        [void]$firstPropertyGroup.InsertBefore($deterministic, $firstPropertyGroup.FirstChild)
        [void]$xml.Save($fullName)
        Write-Output "Updated: $fullName"

    }
} else {
    Write-Output "This command must be run in a top level sln directory"
}


 pause