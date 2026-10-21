$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testRoot=Join-Path (Split-Path $PSScriptRoot -Parent) ('build\zip-path-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force|Out-Null
function New-Fixture([string]$path,[string[]]$names){
    $zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($name in $names){$stream=$zip.CreateEntry($name).Open();try{$bytes=[Text.Encoding]::UTF8.GetBytes('fixture: '+$name);$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}}}finally{$zip.Dispose()}
}
$valid=Join-Path $testRoot 'windows-assets.zip'
New-Fixture $valid @('assets/data\words.tsv','assets/licenses\MIT.txt','assets/index.html','classes.dex')
& (Join-Path $PSScriptRoot 'normalize-zip-paths.ps1') -Path $valid
$zip=[IO.Compression.ZipFile]::OpenRead($valid)
try{
    foreach($name in @('assets/data/words.tsv','assets/licenses/MIT.txt','assets/index.html','classes.dex')){
        $entry=$zip.GetEntry($name)
        if(!$entry){throw ('Missing exact ZIP path: '+$name)}
        $reader=[IO.StreamReader]::new($entry.Open())
        try{$content=$reader.ReadToEnd()}finally{$reader.Dispose()}
        $original=if($name -eq 'assets/data/words.tsv'){'assets/data\words.tsv'}elseif($name -eq 'assets/licenses/MIT.txt'){'assets/licenses\MIT.txt'}else{$name}
        if($content -cne ('fixture: '+$original)){throw ('Content changed: '+$name)}
    }
    if($zip.Entries.Count -ne 4 -or @($zip.Entries|Where-Object {$_.FullName.Contains('\')}).Count){throw 'Unexpected ZIP entries.'}
}finally{$zip.Dispose()}
& (Join-Path $PSScriptRoot 'normalize-zip-paths.ps1') -Path $valid
$collision=Join-Path $testRoot 'collision.zip'
New-Fixture $collision @('assets/data\words.tsv','assets/data/words.tsv')
$rejected=$false
try{& (Join-Path $PSScriptRoot 'normalize-zip-paths.ps1') -Path $collision}catch{if($_.Exception.Message -notlike 'ZIP path collision:*'){throw};$rejected=$true}
if(!$rejected){throw 'Conflicting Android ZIP paths were accepted.'}
$zip=[IO.Compression.ZipFile]::OpenRead($collision)
try{if($zip.Entries.Count -ne 2 -or !$zip.GetEntry('assets/data\words.tsv') -or !$zip.GetEntry('assets/data/words.tsv')){throw 'Collision handling lost source entries.'}}finally{$zip.Dispose()}
Write-Output 'PASS: exact Android paths, unchanged content, repeated normalization and collision rejection'
