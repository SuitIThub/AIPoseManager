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
$types | Where-Object { $_.IsEnum -and $_.Name -eq 'BoneGroup' } | ForEach-Object {
    Write-Output "ENUM $($_.FullName)"
    [System.Enum]::GetNames($_) | ForEach-Object { "  $_" }
}
