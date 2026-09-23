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
# Chỉ yêu cầu những WPF assemblies thực sự phải xuất hiện trong metadata.
# System.Xaml có thể được MSBuild/csc dùng khi compile nhưng bị loại khỏi AssemblyRef nếu source không dùng type trực tiếp.
foreach($req in @('PresentationFramework','PresentationCore','WindowsBase')){
  if($refs -notcontains $req){throw ('WPF_REFERENCE_MISSING|'+$req)}
}

$tfm=$asm.GetCustomAttributesData() |
  Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } |
  Select-Object -First 1
if($null -eq $tfm -or $tfm.ConstructorArguments.Count -lt 1){throw 'TARGET_FRAMEWORK_ATTRIBUTE_MISSING'}
$targetFramework=[string]$tfm.ConstructorArguments[0].Value
if($targetFramework -notmatch '^\.NETFramework,Version=v4\.6'){throw ('TARGET_FRAMEWORK_MISMATCH|'+$targetFramework)}

$lines=@(
  'STATUS=PASS',
  'DLL='+$Dll,
  'SHA256='+$hash,
  'ASSEMBLY='+$asm.FullName,
  'PROGID='+$progid.Value,
  'CLSID='+$guid.Value,
  'VERSION='+$ver,
  'TARGET_FRAMEWORK='+$targetFramework,
  'WPF_REFS='+($refs -join ',')
)
$lines | ForEach-Object { Write-Host $_ }
if(-not [string]::IsNullOrWhiteSpace($Report)){
  [IO.File]::WriteAllLines($Report,$lines,[Text.UTF8Encoding]::new($false))
}
