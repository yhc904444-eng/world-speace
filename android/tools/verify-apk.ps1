param([Parameter(Mandatory=$true)][string]$Path,[string]$AssetsRoot=(Join-Path (Split-Path $PSScriptRoot -Parent) 'app\src\main\assets'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$AssetsRoot=(Resolve-Path -LiteralPath $AssetsRoot).Path.TrimEnd('\','/')
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path).Path)
try{
    # Android AssetManager opens exactly "assets/" + the slash-delimited name.
    # A Windows backslash in a ZIP name is a literal character, not a directory.
    $dictionary=$archive.GetEntry('assets/data/words.tsv')
    if($null -eq $dictionary){throw 'FAILED: Android cannot open assets/data/words.tsv by its exact path.'}
    foreach($entry in $archive.Entries){
        if($entry.FullName.Contains('\')){throw ('FAILED: non-Android ZIP path: '+$entry.FullName)}
        if($entry.FullName -match 'UserData|progress\.json|before-restore|keystore|\.jks$|native-contract|__tests|browser-smoke'){throw ('FAILED: unexpected APK content: '+$entry.FullName)}
    }
    $actualCount=0
    foreach($file in (Get-ChildItem -LiteralPath $AssetsRoot -File -Recurse)){
        $relative=$file.FullName.Substring($AssetsRoot.Length+1).Replace('\','/')
        $name='assets/'+$relative
        $entry=$archive.GetEntry($name)
        if($null -eq $entry){throw ('FAILED: Android cannot open '+$name)}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$embeddedHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
        if($embeddedHash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()){throw ('FAILED: APK asset differs from source: '+$name)}
        $actualCount++
    }
    Write-Output ('PASS: Android exact asset paths and content hashes ('+$actualCount+' assets)')
    Write-Output 'PASS: no backslash ZIP entries or private application data'
}finally{$archive.Dispose()}
