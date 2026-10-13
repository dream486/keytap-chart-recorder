param([string]$LibraryRoot)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$build=Join-Path $workspace 'artifacts\musynx-mod\core-tests'
New-Item -ItemType Directory -Path $build -Force | Out-Null
if(-not $LibraryRoot){$LibraryRoot=Join-Path $workspace 'artifacts\musynx-mod\ExampleLibrary'; & node (Join-Path $workspace 'tools\make-mod-examples.cjs') $LibraryRoot; if($LASTEXITCODE -ne 0){throw 'Example generation failed'}}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=Split-Path $PSScriptRoot
& $compiler /nologo /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $build 'CoreTests.exe')) (Join-Path $source 'Core.cs') (Join-Path $source 'Progress.cs') (Join-Path $PSScriptRoot 'CoreTests.cs')
if($LASTEXITCODE -ne 0){throw 'Core test compilation failed'}
$output=Join-Path $build ([Guid]::NewGuid().ToString('N'))
& (Join-Path $build 'CoreTests.exe') (Join-Path $source 'examples\full.json') (Join-Path $LibraryRoot '00000000000000000000000000000001\audio.wav') $output
if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
