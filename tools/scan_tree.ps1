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
foreach ($name in @('Studio.TreeNodeCtrl', 'Studio.TreeNodeObject', 'Studio.FKCtrl', 'Studio.IKCtrl')) {
    $t = $types | Where-Object { $_.FullName -eq $name } | Select-Object -First 1
    Write-Output "==== $name ===="
    if (-not $t) { Write-Output 'NOT FOUND'; continue }
    $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
    $t.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.Name) $($_.Name)" }
    $t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.Name) $($_.Name)" }
    $t.GetMethods($flags) | Where-Object { -not $_.IsSpecialName -and ($_.Name -match 'Select|Active|Enable') } | Sort-Object Name | ForEach-Object {
        $ps = @($_.GetParameters() | ForEach-Object { $_.ParameterType.Name })
        "METHOD $($_.Name)($([string]::Join(',', $ps)))"
    }
}
