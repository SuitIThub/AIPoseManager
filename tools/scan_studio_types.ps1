$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
$main = Join-Path $managed 'Assembly-CSharp.dll'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try { [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName) } catch {}
}
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($main)
function Get-TypesSafe($assembly) {
    try { return @($assembly.GetTypes()) }
    catch [System.Reflection.ReflectionTypeLoadException] {
        return @($_.Exception.Types | Where-Object { $_ -ne $null })
    }
}
$types = Get-TypesSafe $a
function Dump-Type($fullName) {
    $t = $types | Where-Object { $_.FullName -eq $fullName } | Select-Object -First 1
    if (-not $t) { Write-Output "NOT FOUND: $fullName"; return }
    Write-Output "==== $fullName ===="
    $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
    $t.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.Name) $($_.Name)" }
    $t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.Name) $($_.Name)" }
}

Dump-Type 'Studio.OCIChar+BoneInfo'
Dump-Type 'Studio.GuideObject'
Dump-Type 'Studio.ChangeAmount'
Dump-Type 'Studio.ObjectCtrlInfo'

$studio = $types | Where-Object { $_.FullName -eq 'Studio.Studio' } | Select-Object -First 1
if ($studio) {
    Write-Output '==== Studio.Studio ===='
    $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
    $studio.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.FullName) $($_.Name)" }
    $studio.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.FullName) $($_.Name)" }
    $studio.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly') |
        Where-Object { $_.Name -match 'Select|Tree|Object' } |
        Sort-Object Name |
        ForEach-Object {
            $ps = @($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" })
            "METHOD $($_.Name)($([string]::Join(', ', $ps)))"
        }
}
