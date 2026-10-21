param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open((Resolve-Path -LiteralPath $Path).Path,[IO.Compression.ZipArchiveMode]::Update)
try{
    # Windows aapt2 can emit literal backslashes in nested asset ZIP names.
    # Android AssetManager requires forward slashes. Run only before signing.
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $renames=@()
    foreach($entry in $archive.Entries){
        $normalized=$entry.FullName.Replace('\','/')
        if(!$names.Add($normalized)){throw ('ZIP path collision: '+$normalized)}
        if($normalized -cne $entry.FullName){$renames+=@{Entry=$entry;Name=$normalized}}
    }
    foreach($rename in $renames){
        $original=$rename.Entry
        $replacement=$archive.CreateEntry($rename.Name,[IO.Compression.CompressionLevel]::Optimal)
        $replacement.LastWriteTime=$original.LastWriteTime
        $replacement.ExternalAttributes=$original.ExternalAttributes
        $inputStream=$original.Open();$outputStream=$replacement.Open()
        try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
        $original.Delete()
    }
    Write-Output ('Normalized ZIP paths: '+$renames.Count)
}finally{$archive.Dispose()}
