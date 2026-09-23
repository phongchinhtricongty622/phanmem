param(
  [Parameter(Mandatory=$true)][string]$Dll,
  [string]$Report = ""
)
$ErrorActionPreference='Stop'
$Dll=(Resolve-Path -LiteralPath $Dll).Path
$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $Dll).Hash.ToLowerInvariant()
$asm=[Reflection.Assembly]::LoadFile($Dll)
$type=$asm.GetType('STVBUIHost.STVBUIHost',$true,$false)
$guid=[Runtime.InteropServices.GuidAttribute]($type.GetCustomAttributes([Runtime.InteropServices.GuidAttribute],$false)|Select-Object -First 1)
$progid=[Runtime.InteropServices.ProgIdAttribute]($type.GetCustomAttributes([Runtime.InteropServices.ProgIdAttribute],$false)|Select-Object -First 1)
if($null -eq $guid -or $guid.Value -ne '14762D8B-9DC4-49BD-B33F-739BFA5D21BE'){throw 'CLSID_MISMATCH'}
if($null -eq $progid -or $progid.Value -ne 'STVB.UIHost'){throw 'PROGID_MISMATCH'}
$obj=[Activator]::CreateInstance($type)
if([int]$obj.Ping() -ne 1){throw 'PING_FAILED'}
$ver=[string]$obj.GetVersion()
if([string]::IsNullOrWhiteSpace($ver)){throw 'GETVERSION_EMPTY'}
$refs=$asm.GetReferencedAssemblies()|ForEach-Object{$_.Name}
foreach($req in @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')){
  if($refs -notcontains $req){throw ('WPF_REFERENCE_MISSING|'+$req)}
}
$lines=@(
  'STATUS=PASS',
  'DLL='+$Dll,
  'SHA256='+$hash,
  'ASSEMBLY='+$asm.FullName,
  'PROGID='+$progid.Value,
  'CLSID='+$guid.Value,
  'VERSION='+$ver,
  'WPF_REFS='+($refs -join ',')
)
$lines | ForEach-Object { Write-Host $_ }
if(-not [string]::IsNullOrWhiteSpace($Report)){
  [IO.File]::WriteAllLines($Report,$lines,[Text.UTF8Encoding]::new($false))
}
