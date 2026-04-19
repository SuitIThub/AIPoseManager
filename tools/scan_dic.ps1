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
$types | ForEach-Object {
    $t = $_
    try {
        $bf = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
        $t.GetFields($bf) | Where-Object { $_.Name -match 'dicObjectCtrl|Instance' } | ForEach-Object {
            "$($t.FullName) FIELD $($_.Name) : $($_.FieldType.Name)"
        }
    } catch {}
} | Where-Object { $_ -match 'dicObjectCtrl|Studio\.' } | Select-Object -First 40
