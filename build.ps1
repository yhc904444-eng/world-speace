$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Speech.dll','UIAutomationProvider.dll','UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $wpf $_) }
$compilerArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output','/codepage:65001',('/out:' + (Join-Path $outputDir 'world speace.exe')),('/win32icon:' + (Join-Path $projectRoot 'src\app.ico')),('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')),('/resource:' + (Join-Path $projectRoot 'src\app.ico') + ',app.ico'),('/resource:' + (Join-Path $projectRoot 'src\Theme.xaml') + ',Theme.xaml'),('/resource:' + (Join-Path $projectRoot 'data\words.tsv') + ',words.tsv')) + $references + @((Join-Path $projectRoot 'src\Core.cs'),(Join-Path $projectRoot 'src\Main.cs'),(Join-Path $projectRoot 'src\Tests.cs'),(Join-Path $projectRoot 'src\Motion.cs'),(Join-Path $projectRoot 'src\Startup.cs'),(Join-Path $projectRoot 'src\Glass.cs'),(Join-Path $projectRoot 'src\Appearance.cs'),(Join-Path $projectRoot 'src\AppearanceTests.cs'),(Join-Path $projectRoot 'src\MotionTests.cs'),(Join-Path $projectRoot 'src\AssemblyInfo.cs'))
$compilerArgs += @((Join-Path $projectRoot 'src\ReviewScheduler.cs'),(Join-Path $projectRoot 'src\ReviewTests.cs'))
& (Join-Path $framework 'csc.exe') @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\world speace.exe.config') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'data\ECDICT-LICENSE.txt'),(Join-Path $projectRoot 'data\source.json') -Destination $outputDir -Force
Write-Output (Join-Path $outputDir 'world speace.exe')
