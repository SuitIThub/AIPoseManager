$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$t = $a.GetType('Studio.Studio')
Write-Output "Base: $($t.BaseType.FullName)"
$cur = $t.BaseType
while ($cur) {
    Write-Output "  -> $($cur.FullName)"
    $cur = $cur.BaseType
}
$bf = [System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$t.GetProperties($bf) | Where-Object { $_.Name -eq 'Instance' } | ForEach-Object { "PROP Instance: $($_.PropertyType.FullName)" }
$t.BaseType.GetProperties($bf) | Where-Object { $_.Name -eq 'Instance' } | ForEach-Object { "BASE PROP Instance: $($_.PropertyType.FullName)" }
