$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$t = $a.GetType('Studio.OCIChar+IKInfo')
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
Write-Output "==== $($t.FullName) ===="
$t.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.Name) $($_.Name)" }
$t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.Name) $($_.Name)" }
