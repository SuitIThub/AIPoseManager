$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$t = $a.GetType('Studio.OCIChar')
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
$t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.FullName) $($_.Name)" }
