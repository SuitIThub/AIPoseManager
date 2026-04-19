$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
$main = Join-Path $managed 'Assembly-CSharp.dll'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($main)
$t = ([System.Reflection.Assembly]::ReflectionOnlyLoadFrom($main).GetType('Studio.Studio'))
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
Write-Output '--- Static fields/properties on Studio.Studio ---'
$t.GetFields($flags) | Where-Object { $_.IsStatic } | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.Name) $($_.Name)" }
$t.GetProperties($flags) | Where-Object { $_.GetGetMethod($true).IsStatic } | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.Name) $($_.Name)" }
$t.GetMethods($flags) | Where-Object { $_.IsStatic } | Sort-Object Name | ForEach-Object {
    $ps = @($_.GetParameters() | ForEach-Object { $_.ParameterType.Name })
    "STATIC $($_.Name)($([string]::Join(',', $ps)))"
} | Select-Object -First 40
