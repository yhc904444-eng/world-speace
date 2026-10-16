$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$releaseRoot = Join-Path $projectRoot 'release'
$appDir = Join-Path $releaseRoot '词间'
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
foreach ($name in @('词间.exe','词间.exe.config','ECDICT-LICENSE.txt','source.json')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'dist') $name) -Destination $appDir -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot '使用说明.md') -Destination $appDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'data\语境示例30词.tsv') -Destination $appDir -Force
$template = "word,translation,phonetic,definition,example,example_translation`r`nserendipity,意外发现美好事物的运气,ˌserənˈdɪpəti,A happy discovery made by chance.,Finding this quiet cafe was pure serendipity.,发现这家安静的咖啡馆完全是意外之喜。`r`nresilient,有韧性的；能迅速恢复的,rɪˈzɪliənt,Able to recover after difficulty.,A resilient learner keeps trying after a mistake.,有韧性的学习者会在犯错后继续尝试。`r`n"
[IO.File]::WriteAllText((Join-Path $appDir '自制词书模板.csv'),$template,[Text.UTF8Encoding]::new($true))
$exeHash = (Get-FileHash -LiteralPath (Join-Path $appDir '词间.exe') -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $appDir 'SHA256.txt'),($exeHash + '  词间.exe' + [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
$zipPath = Join-Path $releaseRoot '词间_Windows单机版_v1.3.0.zip'
# Explicit file allowlist: a used portable directory can contain private UserData.
# Never include that directory or its learning records in a distributable archive.
$packageNames = @('词间.exe','词间.exe.config','ECDICT-LICENSE.txt','source.json','使用说明.md','语境示例30词.tsv','自制词书模板.csv','SHA256.txt')
$packageFiles = $packageNames | ForEach-Object { Join-Path $appDir $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $zipPath -Force
$verification = Join-Path $projectRoot 'verification'
New-Item -ItemType Directory -Path $verification -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\self-test-results.txt') -Destination $verification -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\render-tests\ui-test-results.txt') -Destination $verification -Force
foreach($testReport in @('motion-test-results.txt','appearance-test-results.txt','exclusion-test-results.txt','navigation-test-results.txt')) {
    $testPath=Join-Path (Join-Path $projectRoot 'dist') $testReport
    if(Test-Path -LiteralPath $testPath){Copy-Item -LiteralPath $testPath -Destination $verification -Force}
}
$screenshots = Join-Path $verification 'screenshots'
New-Item -ItemType Directory -Path $screenshots -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'dist\render-tests') -Filter '*.png' | Copy-Item -Destination $screenshots -Force
Get-Item -LiteralPath $zipPath,(Join-Path $appDir '词间.exe') | Select-Object FullName,Length
