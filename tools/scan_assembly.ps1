$ErrorActionPreference = 'Stop'
$path = 'D:\Honey Select\StudioNEOV2_Data\Managed\Assembly-CSharp.dll'
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
function Get-TypesSafe($assembly) {
    try { return @($assembly.GetTypes()) }
    catch [System.Reflection.ReflectionTypeLoadException] {
        return @($_.Exception.Types | Where-Object { $_ -ne $null })
    }
}
$types = Get-TypesSafe $a
$patterns = @('OCIChar', 'GuideObject', 'TreeNodeObject', 'Studio', 'BoneInfo', 'ChangeAmount')
foreach ($p in $patterns) {
    Write-Output "--- $p ---"
    $types |
        Where-Object { $_.FullName -match $p } |
        ForEach-Object { $_.FullName } |
        Select-Object -First 50
}
