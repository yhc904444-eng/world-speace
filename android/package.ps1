$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw|ConvertFrom-Json).version
$artifactName='world-speace_Android_v'+$version
$windowsRoot=Split-Path $projectRoot -Parent
$releaseRoot=if(Test-Path -LiteralPath (Join-Path $windowsRoot 'src\Core.cs')){Join-Path $windowsRoot 'release'}else{Join-Path $projectRoot 'release'}
New-Item -ItemType Directory -Path $releaseRoot -Force|Out-Null
$apk=Join-Path $releaseRoot ($artifactName+'.apk')
if(!(Test-Path -LiteralPath $apk)){throw 'Run build.ps1 first.'}
$packageRoot=Join-Path $projectRoot ('build\source-package-'+[Guid]::NewGuid().ToString('N'))
$stage=Join-Path $packageRoot 'world-speace-android'
New-Item -ItemType Directory -Path $stage -Force|Out-Null
# Explicit allowlist. Never copy .signing, build, private app data or test output.
foreach($name in @('app','tests','tools','docs')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $stage $name) -Recurse}
foreach($name in @('README.md','package.json','.gitignore','build.ps1','package.ps1')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $stage}
$zip=Join-Path $releaseRoot ($artifactName+'-Source.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
& (Join-Path $PSScriptRoot 'tools\normalize-zip-paths.ps1') -Path $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{if(@($archive.Entries|Where-Object {$_.FullName -match '\.signing|\.jks$|keystore|UserData|progress\.json|before-restore\.json|android-backup\.json|windows-roundtrip\.json|/verification/'}).Count){throw 'Private or generated data found in source package.'}}finally{$archive.Dispose()}
$apkHash=(Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $releaseRoot ($artifactName+'-SHA256.txt')),($apkHash+'  '+(Split-Path $apk -Leaf)+"`n"+$sourceHash+'  '+(Split-Path $zip -Leaf)+"`n"),[Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $apk,$zip|Select-Object Name,Length
