param(
    [ValidateSet('Install','Uninstall','Status')][string]$Mode='Install',
    [string]$GameRoot,
    [string]$PackageRoot=$PSScriptRoot
)
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PackageRoot)
$spec=Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
if(-not $GameRoot){
    $steamPaths=@('C:\Program Files (x86)\Steam')
    foreach($registryPath in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')){
        try{$reg=Get-ItemProperty $registryPath -ErrorAction Stop;if($reg.SteamPath){$steamPaths+=$reg.SteamPath};if($reg.InstallPath){$steamPaths+=$reg.InstallPath}}catch{}
    }
    $libraries=@($steamPaths)
    foreach($steamPath in $steamPaths){$vdf=Join-Path $steamPath 'steamapps\libraryfolders.vdf';if(Test-Path -LiteralPath $vdf){$raw=Get-Content -LiteralPath $vdf -Raw;foreach($match in [regex]::Matches($raw,'"path"\s+"([^"]+)"')){$libraries+=$match.Groups[1].Value.Replace('\\','\')}}}
    $found=@($libraries | Select-Object -Unique | ForEach-Object {Join-Path $_ 'steamapps\common\MUSYNX'} | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'MUSYNX.exe')})
    if($found.Count -ne 1){throw '未唯一定位 Steam 原版 MUSYNX。请使用 -GameRoot 指定其安装目录。'}
    $GameRoot=$found[0]
}
$game=[IO.Path]::GetFullPath($GameRoot).TrimEnd('\')
if(-not(Test-Path -LiteralPath (Join-Path $game 'MUSYNX.exe'))){throw '指定目录没有 MUSYNX.exe'}
function Target([string]$Relative){$p=[IO.Path]::GetFullPath((Join-Path $game $Relative));if(-not $p.StartsWith($game+'\',[StringComparison]::OrdinalIgnoreCase)){throw '目标路径超出游戏目录'};return $p}
$dll=Target 'MUSYNX_Data\Managed\Assembly-CSharp.dll'
$audio=Target 'MUSYNX_Data\globalgamemanagers'
$modRoot=Target 'CustomChartMod'
$recordPath=Join-Path $modRoot 'installation.json'
$record=if(Test-Path -LiteralPath $recordPath){Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json}else{$null}
if($Mode -eq 'Status'){
    if(-not $record){Write-Output '未安装敲谱曲库 Mod';return}
    foreach($item in $record.nativeFiles){[pscustomobject]@{File=$item.relativePath;MatchesInstalled=(Hash (Target $item.relativePath)) -eq $item.installedSha256}}
    foreach($item in $record.modFiles){[pscustomobject]@{File=$item.relativePath;MatchesInstalled=(Test-Path -LiteralPath (Target $item.relativePath)) -and (Hash (Target $item.relativePath)) -eq $item.sha256}}
    return
}
if(Get-Process -Name MUSYNX -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $game 'MUSYNX.exe')}){throw '请先关闭游戏，再安装或卸载 Mod。'}
if($Mode -eq 'Uninstall'){
    if(-not $record){throw '未找到该 Mod 的安装记录'}
    foreach($item in $record.nativeFiles){$actual=Hash (Target $item.relativePath);if($actual -ne $item.installedSha256 -and $actual -ne $item.originalSha256){throw '游戏文件已变化，停止覆盖；请检查是否更新过游戏。'};if((Hash (Target $item.backupPath)) -ne $item.originalSha256){throw '原文件备份校验失败'}}
    foreach($item in $record.modFiles){if((Test-Path -LiteralPath (Target $item.relativePath)) -and (Hash (Target $item.relativePath)) -ne $item.sha256){throw 'Mod 文件已被修改，停止移除'}}
    foreach($item in $record.nativeFiles){Copy-Item -LiteralPath (Target $item.backupPath) -Destination (Target $item.relativePath) -Force}
    foreach($item in $record.modFiles){$path=Target $item.relativePath;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
    Move-Item -LiteralPath $recordPath -Destination (Join-Path $modRoot ('uninstalled-'+[guid]::NewGuid().ToString('N')+'.json'))
    Write-Output '已恢复原游戏。CustomCharts 曲库保留，可再次安装后继续使用。';return
}
foreach($item in $spec.payload){$source=Join-Path $package ('payload\'+$item.name);if((Hash $source) -ne $item.sha256){throw "安装包校验失败：$source"}}
$dllHash=Hash $dll;$audioHash=Hash $audio
$baseDll=$dll;$baseAudio=$audio
if($record){
    foreach($item in $record.nativeFiles){if((Hash (Target $item.relativePath)) -ne $item.installedSha256){throw '已安装文件与记录不符，停止覆盖'}}
    foreach($item in $record.modFiles){if(-not(Test-Path -LiteralPath (Target $item.relativePath)) -or (Hash (Target $item.relativePath)) -ne $item.sha256){throw '已安装 Mod 文件与记录不符，停止覆盖'}}
    $baseDll=Target $record.nativeFiles[0].backupPath;$baseAudio=Target $record.nativeFiles[1].backupPath
}
if((Hash $baseDll) -ne $spec.originalAssemblySha256){throw '此包适配 Steam 原版 Build 25570400；程序集不匹配，停止安装。'}
if((Hash $baseAudio) -ne $spec.audio.originalSha256){throw '音频配置文件不匹配，停止安装。'}
if(-not $record){foreach($item in $spec.modFiles){if(Test-Path -LiteralPath (Target $item.relativePath)){throw "已存在同名文件，停止覆盖：$($item.relativePath)"}}}
$backup=Join-Path $modRoot 'Backup';New-Item -ItemType Directory -Path $backup -Force | Out-Null
if(-not $record){Copy-Item -LiteralPath $baseDll -Destination (Join-Path $backup 'Assembly-CSharp.dll');Copy-Item -LiteralPath $baseAudio -Destination (Join-Path $backup 'globalgamemanagers')}
$baseDll=Join-Path $backup 'Assembly-CSharp.dll';$baseAudio=Join-Path $backup 'globalgamemanagers'
if((Hash $baseDll) -ne $spec.originalAssemblySha256 -or (Hash $baseAudio) -ne $spec.audio.originalSha256){throw '备份校验失败，未修改游戏'}
$stage=Join-Path $modRoot ('Stage-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $stage | Out-Null
$stagedDll=Join-Path $stage 'Assembly-CSharp.dll';$stagedAudio=Join-Path $stage 'globalgamemanagers'
Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'previous-Assembly-CSharp.dll')
Copy-Item -LiteralPath $audio -Destination (Join-Path $stage 'previous-globalgamemanagers')
if($record){foreach($item in $record.modFiles){$previous=Join-Path $stage ('previous\'+$item.relativePath);New-Item -ItemType Directory -Path (Split-Path $previous) -Force | Out-Null;Copy-Item -LiteralPath (Target $item.relativePath) -Destination $previous}}
& (Join-Path $package 'payload\Patcher.exe') $baseDll (Join-Path $package 'payload\ChartRecorderMod.dll') $stagedDll
if($LASTEXITCODE -ne 0){throw '补丁生成失败，未修改游戏'}
$bytes=[IO.File]::ReadAllBytes($baseAudio);if($bytes[$spec.audio.offset] -ne 1){throw '音频配置偏移不匹配'};$bytes[$spec.audio.offset]=0;[IO.File]::WriteAllBytes($stagedAudio,$bytes)
if((Hash $stagedAudio) -ne $spec.audio.patchedSha256){throw '音频配置补丁校验失败'}
$written=@()
try{
    foreach($item in $spec.modFiles){$target=Target $item.relativePath;New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $package ('payload\'+$item.name)) -Destination $target -Force;$written+=$target}
    Copy-Item -LiteralPath $stagedDll -Destination $dll -Force;Copy-Item -LiteralPath $stagedAudio -Destination $audio -Force
    $native=@([pscustomobject]@{relativePath='MUSYNX_Data\Managed\Assembly-CSharp.dll';backupPath='CustomChartMod\Backup\Assembly-CSharp.dll';originalSha256=$spec.originalAssemblySha256;installedSha256=Hash $dll},[pscustomobject]@{relativePath='MUSYNX_Data\globalgamemanagers';backupPath='CustomChartMod\Backup\globalgamemanagers';originalSha256=$spec.audio.originalSha256;installedSha256=Hash $audio})
    $added=@($spec.modFiles | ForEach-Object {[pscustomobject]@{relativePath=$_.relativePath;sha256=Hash (Target $_.relativePath)}})
    [pscustomobject]@{version=$spec.version;nativeFiles=$native;modFiles=$added} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding utf8
}catch{
    Copy-Item -LiteralPath (Join-Path $stage 'previous-Assembly-CSharp.dll') -Destination $dll -Force;Copy-Item -LiteralPath (Join-Path $stage 'previous-globalgamemanagers') -Destination $audio -Force
    if($record){foreach($item in $record.modFiles){Copy-Item -LiteralPath (Join-Path $stage ('previous\'+$item.relativePath)) -Destination (Target $item.relativePath) -Force}}else{foreach($path in $written){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}}
    throw
}
$library=Target 'CustomCharts\Library'
if(-not(Test-Path -LiteralPath (Join-Path $library 'library.json')) -and (Test-Path -LiteralPath (Join-Path $package 'ExampleLibrary\library.json'))){New-Item -ItemType Directory -Path $library -Force | Out-Null;Get-ChildItem -LiteralPath (Join-Path $package 'ExampleLibrary') | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $library -Recurse}}
Write-Output '安装成功。正常启动 MUSYNX，在选歌界面按 F8 打开敲谱曲库。'
