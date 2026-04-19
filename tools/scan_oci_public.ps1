$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$t = $a.GetType('Studio.OCIChar')
$flags = [System.Reflection.BindingFlags]'Public,Instance,DeclaredOnly'
$t.GetMethods($flags) | Where-Object { $_.Name -match 'Active' } | ForEach-Object {
    "$($_.Name)($($_.GetParameters() | ForEach-Object { $_.ParameterType.Name }))"
}
