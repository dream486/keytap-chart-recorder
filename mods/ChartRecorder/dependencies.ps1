param([string]$DependenciesRoot=(Join-Path $PSScriptRoot '..\..\.tmp\mod-dependencies'))
$ErrorActionPreference='Stop'
$DependenciesRoot=[IO.Path]::GetFullPath($DependenciesRoot)
New-Item -ItemType Directory -Path $DependenciesRoot -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
foreach($spec in @(@{id='naudio';version='1.8.0';sha='8E347ABC9301D67DD7493A0FBBE5CC1F912900C204A84220CC8CDF0E0B8DF0DE';dll='lib\net35\NAudio.dll'},@{id='mono.cecil';version='0.11.6';sha='C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B';dll='lib\net40\Mono.Cecil.dll'})){
    $name=$spec.id+'.'+$spec.version
    $folder=Join-Path $DependenciesRoot $name
    if(-not(Test-Path -LiteralPath (Join-Path $folder $spec.dll))){
        $url='https://api.nuget.org/v3-flatcontainer/'+$spec.id+'/'+$spec.version+'/'+$name+'.nupkg'
        $archive=Join-Path $DependenciesRoot ($name+'.zip')
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
        Expand-Archive -LiteralPath $archive -DestinationPath $folder -Force
    }
    if(-not(Test-Path -LiteralPath (Join-Path $folder $spec.dll))){throw ('Missing package DLL: '+$name)}
    if((Get-FileHash -LiteralPath (Join-Path $folder $spec.dll)).Hash -ne $spec.sha){throw ('Dependency checksum mismatch: '+$name)}
    [pscustomobject]@{Package=$name;Path=(Join-Path $folder $spec.dll);Sha256=(Get-FileHash -LiteralPath (Join-Path $folder $spec.dll)).Hash}
}
