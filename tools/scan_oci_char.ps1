$ErrorActionPreference = 'Stop'
$managed = 'D:\Honey Select\StudioNEOV2_Data\Managed'
$main = Join-Path $managed 'Assembly-CSharp.dll'
$loaded = New-Object System.Collections.Generic.List[string]
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try {
        [void][System.Reflection.Assembly]::ReflectionOnlyLoadFrom($_.FullName)
        $loaded.Add($_.Name)
    } catch {
        # ignore assemblies that cannot be reflection-loaded
    }
}
Write-Output "Preloaded $($loaded.Count) assemblies"
$a = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($main)
function Get-TypesSafe($assembly) {
    try { return @($assembly.GetTypes()) }
    catch [System.Reflection.ReflectionTypeLoadException] {
        return @($_.Exception.Types | Where-Object { $_ -ne $null })
    }
}
$types = Get-TypesSafe $a
$t = $types | Where-Object { $_.FullName -eq 'Studio.OCIChar' } | Select-Object -First 1
if (-not $t) {
    $types | Where-Object { $_.FullName -like 'Studio.OCIChar*' } | ForEach-Object { $_.FullName } | Select-Object -First 30
    Write-Output 'Studio.OCIChar not found'
    exit 1
}
Write-Output "Type: $($t.FullName)"
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'
$t.GetFields($flags) | Sort-Object Name | ForEach-Object { "FIELD $($_.FieldType.FullName) $($_.Name)" }
$t.GetProperties($flags) | Sort-Object Name | ForEach-Object { "PROP $($_.PropertyType.FullName) $($_.Name)" }
$t.GetMethods($flags) | Where-Object { -not $_.IsSpecialName } | Sort-Object Name | ForEach-Object {
    $ps = @($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" })
    $plist = [string]::Join(', ', $ps)
    "METHOD $($_.ReturnType.Name) $($_.Name)($plist)"
}
