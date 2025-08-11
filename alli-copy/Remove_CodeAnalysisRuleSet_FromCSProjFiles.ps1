cd $PSScriptRoot


gci -Filter *.csproj -Recurse | ForEach-Object {

    $path = $_.FullName

    [xml]$xml=Get-Content $path

    [System.Xml.XmlNamespaceManager]$ns = $xml.NameTable
    $ns.AddNamespace("Any", $xml.DocumentElement.NamespaceURI)

    $nodes = $xml.SelectNodes('//Any:CodeAnalysisRuleSet', $ns)

    Write-Output "Found $($nodes.Count) CodeAnalysisRuleSet Tags for file: $path "

    if($nodes.Count -gt 0){
        foreach($CodeAnalysisRuleSet in $nodes) {
            [void]$CodeAnalysisRuleSet.ParentNode.RemoveChild($CodeAnalysisRuleSet)
        }
        $xml.Save($path) 
    }


}