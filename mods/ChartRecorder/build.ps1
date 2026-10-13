param(
    [string]$GameRoot,
    [string]$DependenciesRoot=(Join-Path $PSScriptRoot '..\..\.tmp\mod-dependencies'),
    [string]$OriginalAssembly
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(-not $GameRoot){
    $libraries=@('C:\Program Files (x86)\Steam')
    try{$steam=(Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath;if($steam){$libraries+=$steam}}catch{}
    foreach($steam in @($libraries)){$vdf=Join-Path $steam 'steamapps\libraryfolders.vdf';if(Test-Path -LiteralPath $vdf){foreach($match in [regex]::Matches([IO.File]::ReadAllText($vdf),'"path"\s+"([^"]+)"')){$libraries+=$match.Groups[1].Value.Replace('\\','\')}}}
    $found=@($libraries | Select-Object -Unique | ForEach-Object {Join-Path $_ 'steamapps\common\MUSYNX'} | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'MUSYNX.exe')})
    if($found.Count -ne 1){throw 'Specify -GameRoot for your Steam original MUSYNX installation'}
    $GameRoot=$found[0]
}
$GameRoot=[IO.Path]::GetFullPath($GameRoot)
$managed=Join-Path $GameRoot 'MUSYNX_Data\Managed'
$originalHash='C3A4811D45228DDF3A70C316E12BC63F3BF2C11C44DC62569D6060C4886B3EB3'
if(-not $OriginalAssembly){foreach($candidate in @((Join-Path $GameRoot 'CustomChartMod\Backup\Assembly-CSharp.dll'),(Join-Path $managed 'Assembly-CSharp.dll'))){if((Test-Path -LiteralPath $candidate) -and (Get-FileHash -LiteralPath $candidate).Hash -eq $originalHash){$OriginalAssembly=$candidate;break}}}
if(-not $OriginalAssembly -or (Get-FileHash -LiteralPath $OriginalAssembly).Hash -ne $originalHash){throw 'Need the verified original Assembly-CSharp.dll for MUSYNX Build 25570400'}
$DependenciesRoot=[IO.Path]::GetFullPath($DependenciesRoot)
$naudio=Join-Path $DependenciesRoot 'naudio.1.8.0\lib\net35\NAudio.dll'
$cecil=Join-Path $DependenciesRoot 'mono.cecil.0.11.6\lib\net40\Mono.Cecil.dll'
foreach($file in @($naudio,$cecil)){if(-not(Test-Path -LiteralPath $file)){throw 'Run mods/ChartRecorder/dependencies.ps1 first'}}
$build=Join-Path $workspace 'artifacts\musynx-mod\build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references=@('UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll','UnityEngine.InputModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.UI.dll','Assembly-CSharp-firstpass.dll') | ForEach-Object { '/reference:'+(Join-Path $managed $_) }
& $compiler /nologo /target:library ('/out:'+(Join-Path $build 'ChartRecorderMod.dll')) @references (Join-Path $PSScriptRoot 'Core.cs') (Join-Path $PSScriptRoot 'Progress.cs') (Join-Path $PSScriptRoot 'GameChartMod.cs')
if($LASTEXITCODE -ne 0){throw 'Mod compile failed'}
Copy-Item -LiteralPath $naudio -Destination (Join-Path $build 'NAudio.dll') -Force
& $compiler /nologo /target:winexe ('/out:'+(Join-Path $build 'ImportHelper.exe')) ('/reference:'+$naudio) /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'ImportHelper.cs')
if($LASTEXITCODE -ne 0){throw 'Import helper compile failed'}
Copy-Item -LiteralPath $cecil -Destination (Join-Path $build 'Mono.Cecil.dll') -Force
& $compiler /nologo ('/out:'+(Join-Path $build 'Patcher.exe')) ('/reference:'+$cecil) (Join-Path $PSScriptRoot 'Patcher.cs')
if($LASTEXITCODE -ne 0){throw 'Patcher compile failed'}
& (Join-Path $build 'Patcher.exe') $OriginalAssembly (Join-Path $build 'ChartRecorderMod.dll') (Join-Path $build 'Assembly-CSharp.dll')
if($LASTEXITCODE -ne 0){throw 'Patching failed'}
Write-Output $build
