$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
function Get-TypesSafe($assembly) {
    try { return @($assembly.GetTypes()) }
    catch [System.Reflection.ReflectionTypeLoadException] {
        return @($_.Exception.Types | Where-Object { $_ -ne $null })
    }
}
$types = Get-TypesSafe $a
foreach ($name in @('Studio.BoneGroup', 'Studio.OICharInfo', 'Studio.KinematicMode')) {
    $t = $types | Where-Object { $_.FullName -eq $name } | Select-Object -First 1
    Write-Output "==== $name ===="
    if (-not $t) { Write-Output 'NOT FOUND'; continue }
    if ($t.IsEnum) {
        [System.Enum]::GetNames($t) | ForEach-Object { "$_ = $([int][System.Enum]::Parse($t, $_))" }
    } else {
        $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
        $t.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.Name) $($_.Name)" }
        $t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.Name) $($_.Name)" }
    }
}
