param(
    [Parameter(Mandatory = $true)][string]$Path
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$entries = [ordered]@{}
$zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
foreach ($e in $zip.Entries) {
    $ms = New-Object System.IO.MemoryStream
    $s = $e.Open()
    $s.CopyTo($ms)
    $s.Close()
    $entries[$e.FullName] = $ms.ToArray()
    $ms.Dispose()
}
$zip.Dispose()
Write-Output ("read entries: {0}" -f ($entries.Keys -join ', '))

$tmp = "$Path.new"
$fs = [System.IO.File]::Open($tmp, [System.IO.FileMode]::Create)
$za = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
foreach ($name in $entries.Keys) {
    $data = $entries[$name]
    $entry = $za.CreateEntry($name, [System.IO.Compression.CompressionLevel]::NoCompression)
    $es = $entry.Open()
    if ($data.Length -gt 0) { $es.Write($data, 0, $data.Length) }
    $es.Close()
}
$za.Dispose()
$fs.Close()

Move-Item -Force $tmp $Path

$check = [System.IO.File]::ReadAllBytes($Path)
$method = [BitConverter]::ToUInt16($check, 8)
Write-Output ("repacked store-only: {0} bytes, local compression method = {1}" -f $check.Length, $method)