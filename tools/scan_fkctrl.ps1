$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
foreach ($name in @('Studio.FKCtrl', 'Studio.IKCtrl')) {
    $t = $a.GetType($name)
    Write-Output "==== $name ===="
    $flags = [System.Reflection.BindingFlags]'Public,Instance,DeclaredOnly'
    $t.GetMethods($flags) | Sort-Object Name | ForEach-Object {
        "METHOD $($_.Name)"
    }
}
