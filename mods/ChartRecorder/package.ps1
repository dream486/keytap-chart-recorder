param([switch]$Archive)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$build=Join-Path $workspace 'artifacts\musynx-mod\build'
$package=Join-Path $workspace 'artifacts\musynx-mod\MUSYNX-敲谱曲库-Mod'
$package=[IO.Path]::GetFullPath($package)
$expectedParent=[IO.Path]::GetFullPath((Join-Path $workspace 'artifacts\musynx-mod'))+'\'
if(-not $package.StartsWith($expectedParent,[StringComparison]::OrdinalIgnoreCase)){throw 'Package path outside generated artifact directory'}
if(Test-Path -LiteralPath $package){Remove-Item -LiteralPath $package -Recurse -Force}

New-Item -ItemType Directory -Path $package,(Join-Path $package 'payload'),(Join-Path $package 'licenses'),(Join-Path $package 'Source') -Force | Out-Null
$names=@('ChartRecorderMod.dll','ImportHelper.exe','NAudio.dll','Patcher.exe','Mono.Cecil.dll')
$payload=foreach($name in $names){Copy-Item -LiteralPath (Join-Path $build $name) -Destination (Join-Path $package ('payload\'+$name)) -Force;[pscustomobject]@{name=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $build $name) -Algorithm SHA256).Hash}}
$audio=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'audio-config.json') -Raw | ConvertFrom-Json
$files=@([pscustomobject]@{name='ChartRecorderMod.dll';relativePath='MUSYNX_Data\Managed\ChartRecorderMod.dll'},[pscustomobject]@{name='ImportHelper.exe';relativePath='CustomChartMod\ImportHelper.exe'},[pscustomobject]@{name='NAudio.dll';relativePath='CustomChartMod\NAudio.dll'})
[pscustomobject]@{version='0.2.0';originalAssemblySha256='C3A4811D45228DDF3A70C316E12BC63F3BF2C11C44DC62569D6060C4886B3EB3';audio=$audio;payload=@($payload);modFiles=$files} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'package.json') -Encoding utf8
[IO.File]::WriteAllText((Join-Path $package 'Install.ps1'),[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Install.ps1')),(New-Object Text.UTF8Encoding($true)))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $package -Force
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'licenses') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $package 'licenses') -Force}
Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object {$_.Extension -in '.cs','.ps1'} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $package 'Source') -Force}
$seed=Join-Path $package 'ExampleLibrary';New-Item -ItemType Directory -Path $seed -Force | Out-Null
$exampleLibrary=Join-Path $workspace 'artifacts\musynx-mod\ExampleLibrary'
& node (Join-Path $workspace 'tools\make-mod-examples.cjs') $exampleLibrary
if($LASTEXITCODE -ne 0){throw 'Example generation failed'}
Copy-Item -Path (Join-Path $exampleLibrary '*') -Destination $seed -Recurse -Force
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE') -Destination $package -Force
@'
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -Mode Install
pause
'@ | Set-Content -LiteralPath (Join-Path $package '安装.bat') -Encoding ascii
@'
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -Mode Uninstall
pause
'@ | Set-Content -LiteralPath (Join-Path $package '卸载.bat') -Encoding ascii
if($Archive){$dist=Join-Path $workspace 'dist';New-Item -ItemType Directory -Path $dist -Force | Out-Null;Compress-Archive -LiteralPath $package -DestinationPath (Join-Path $dist 'MUSYNX-敲谱曲库-Mod-0.2.0.zip') -Force}
Write-Output $package
