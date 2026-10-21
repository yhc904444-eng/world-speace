param([string]$AndroidSdk=$env:ANDROID_HOME,[string]$JavaHome=$env:JAVA_HOME)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw|ConvertFrom-Json).version
$artifactName='world-speace_Android_v'+$version
$workspaceRoot=Split-Path (Split-Path $projectRoot -Parent) -Parent
# Reuse an installed toolchain without modifying that project or system settings.
$localToolchain=Join-Path $workspaceRoot 'river-poker\mobile\.toolchains'
if(!$AndroidSdk){$AndroidSdk=$env:ANDROID_SDK_ROOT}
if(!$AndroidSdk -and (Test-Path (Join-Path $localToolchain 'android-sdk'))){$AndroidSdk=Join-Path $localToolchain 'android-sdk'}
if(!$JavaHome){$installed=Get-ChildItem -LiteralPath (Join-Path $localToolchain 'java') -Directory -ErrorAction SilentlyContinue|Select-Object -First 1;if($installed){$JavaHome=$installed.FullName}}
if(!$AndroidSdk -or !$JavaHome){throw 'Pass -AndroidSdk (SDK platform 36 / build-tools 36.0.0) and -JavaHome (JDK 21).'}
$tools=Join-Path $AndroidSdk 'build-tools\36.0.0'
$platform=Join-Path $AndroidSdk 'platforms\android-36\android.jar'
$javaBin=Join-Path $JavaHome 'bin'
foreach($required in @($platform,(Join-Path $tools 'aapt2.exe'),(Join-Path $tools 'lib\d8.jar'),(Join-Path $tools 'lib\apksigner.jar'),(Join-Path $javaBin 'javac.exe'))){if(!(Test-Path -LiteralPath $required)){throw "Missing build tool: $required"}}
$runId=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$buildRoot=Join-Path $projectRoot ('build\'+$runId)
foreach($folder in @('classes','dex','generated','staging','resources')){New-Item -ItemType Directory -Path (Join-Path $buildRoot $folder) -Force|Out-Null}
$main=Join-Path $projectRoot 'app\src\main'
$manifest=[xml](Get-Content -LiteralPath (Join-Path $main 'AndroidManifest.xml') -Raw)
$androidNamespace='http://schemas.android.com/apk/res/android'
if($manifest.manifest.GetAttribute('versionName',$androidNamespace) -cne $version){throw 'Manifest and package versions differ.'}
$versionCode=[int]$manifest.manifest.GetAttribute('versionCode',$androidNamespace)
$assets=Join-Path $main 'assets'
New-Item -ItemType Directory -Path (Join-Path $assets 'data'),(Join-Path $assets 'licenses'),(Join-Path $main 'res\drawable') -Force|Out-Null
$windowsRoot=Split-Path $projectRoot -Parent
function Copy-Resource([string]$relativeSource,[string]$relativeTarget){$source=Join-Path $windowsRoot $relativeSource;$target=Join-Path $assets $relativeTarget;if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination $target -Force}elseif(!(Test-Path -LiteralPath $target)){throw "Missing bundled resource: $relativeTarget"}}
Copy-Resource 'data\words.tsv' 'data\words.tsv'
Copy-Resource 'data\source.json' 'data\source.json'
Copy-Resource 'data\ECDICT-LICENSE.txt' 'licenses\ECDICT-MIT.txt'
Copy-Resource 'licenses\FSRS-MIT.txt' 'licenses\FSRS-MIT.txt'
Copy-Resource 'verification\app-icon.png' 'icon.png'
Copy-Item -LiteralPath (Join-Path $assets 'icon.png') -Destination (Join-Path $main 'res\drawable\app_icon.png') -Force
function Invoke-Checked([string]$executable,[string[]]$arguments){& $executable @arguments;if($LASTEXITCODE -ne 0){throw "Build step failed: $executable (exit $LASTEXITCODE)"}}
foreach($resource in (Get-ChildItem -LiteralPath (Join-Path $main 'res') -File -Recurse)){Invoke-Checked (Join-Path $tools 'aapt2.exe') @('compile',$resource.FullName,'-o',(Join-Path $buildRoot 'resources'))}
$compiledResources=@(Get-ChildItem -LiteralPath (Join-Path $buildRoot 'resources') -Filter '*.flat' -File|ForEach-Object FullName)
Invoke-Checked (Join-Path $tools 'aapt2.exe') (@('link','-I',$platform,'--manifest',(Join-Path $main 'AndroidManifest.xml'),'--java',(Join-Path $buildRoot 'generated'),'-A',$assets,'-o',(Join-Path $buildRoot 'base.apk'))+$compiledResources)
$javaSources=@(Get-ChildItem -LiteralPath (Join-Path $main 'java'),(Join-Path $buildRoot 'generated') -Filter '*.java' -Recurse -File|ForEach-Object FullName)
Invoke-Checked (Join-Path $javaBin 'javac.exe') (@('-J-Dfile.encoding=UTF-8','-encoding','UTF-8','-source','8','-target','8','-Xlint:-options','-bootclasspath',((Join-Path $tools 'core-lambda-stubs.jar')+';'+$platform),'-d',(Join-Path $buildRoot 'classes'))+$javaSources)
Invoke-Checked (Join-Path $javaBin 'jar.exe') @('cf',(Join-Path $buildRoot 'classes.jar'),'-C',(Join-Path $buildRoot 'classes'),'.')
Invoke-Checked (Join-Path $javaBin 'java.exe') @('-cp',(Join-Path $tools 'lib\d8.jar'),'com.android.tools.r8.D8','--lib',$platform,'--min-api','26','--output',(Join-Path $buildRoot 'dex'),(Join-Path $buildRoot 'classes.jar'))
Invoke-Checked (Join-Path $javaBin 'jar.exe') @('uf',(Join-Path $buildRoot 'base.apk'),'-C',(Join-Path $buildRoot 'dex'),'classes.dex')
& (Join-Path $projectRoot 'tools\normalize-zip-paths.ps1') -Path (Join-Path $buildRoot 'base.apk')
Invoke-Checked (Join-Path $tools 'zipalign.exe') @('-f','4',(Join-Path $buildRoot 'base.apk'),(Join-Path $buildRoot 'aligned.apk'))
$signingRoot=Join-Path $projectRoot '.signing'
New-Item -ItemType Directory -Path $signingRoot -Force|Out-Null
$keyStore=Join-Path $signingRoot 'world-speace-release.jks'
$passwordFile=Join-Path $signingRoot 'keystore-password.txt'
if(!(Test-Path -LiteralPath $keyStore)){
    if(!(Test-Path -LiteralPath $passwordFile)){[byte[]]$passwordBytes=New-Object byte[] 32;$random=[Security.Cryptography.RandomNumberGenerator]::Create();$random.GetBytes($passwordBytes);$random.Dispose();[IO.File]::WriteAllText($passwordFile,[Convert]::ToBase64String($passwordBytes))}
    $env:WORLD_SPEACE_SIGNING_PASSWORD=[IO.File]::ReadAllText($passwordFile)
    Invoke-Checked (Join-Path $javaBin 'keytool.exe') @('-genkeypair','-keystore',$keyStore,'-alias','world-speace','-storepass:env','WORLD_SPEACE_SIGNING_PASSWORD','-keypass:env','WORLD_SPEACE_SIGNING_PASSWORD','-keyalg','RSA','-keysize','3072','-validity','10000','-dname','CN=world speace Android,O=world speace,C=CN')
}
if(!(Test-Path -LiteralPath $passwordFile)){throw 'Signing password is missing; restore your local signing material before building.'}
$env:WORLD_SPEACE_SIGNING_PASSWORD=[IO.File]::ReadAllText($passwordFile)
$releaseRoot=if(Test-Path -LiteralPath (Join-Path $windowsRoot 'src\Core.cs')){Join-Path $windowsRoot 'release'}else{Join-Path $projectRoot 'release'}
New-Item -ItemType Directory -Path $releaseRoot -Force|Out-Null
$output=Join-Path $releaseRoot ($artifactName+'.apk')
try{Invoke-Checked (Join-Path $javaBin 'java.exe') @('-jar',(Join-Path $tools 'lib\apksigner.jar'),'sign','--ks',$keyStore,'--ks-key-alias','world-speace','--ks-pass','env:WORLD_SPEACE_SIGNING_PASSWORD','--key-pass','env:WORLD_SPEACE_SIGNING_PASSWORD','--out',$output,(Join-Path $buildRoot 'aligned.apk'))}finally{$env:WORLD_SPEACE_SIGNING_PASSWORD=$null}
Invoke-Checked (Join-Path $javaBin 'java.exe') @('-jar',(Join-Path $tools 'lib\apksigner.jar'),'verify','--verbose','--print-certs',$output)
Invoke-Checked (Join-Path $tools 'zipalign.exe') @('-c','4',$output)
& (Join-Path $projectRoot 'tools\verify-apk.ps1') -Path $output -AssetsRoot $assets
$metadata=[ordered]@{app='world speace';platform='Android';versionName=$version;versionCode=$versionCode;minSdk=26;targetSdk=35;compileSdk=36;apk=(Split-Path $output -Leaf);sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant();dictionarySha256=(Get-FileHash -LiteralPath (Join-Path $assets 'data\words.tsv') -Algorithm SHA256).Hash.ToLowerInvariant();buildDirectory=$buildRoot}
[IO.File]::WriteAllText((Join-Path $releaseRoot ($artifactName+'-verification.json')),($metadata|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output ('APK: '+$output)
