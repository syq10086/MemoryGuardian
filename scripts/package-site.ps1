param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'orb\MemoryOrb.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$site = Join-Path $root 'website\memory-guardian'
$downloads = Join-Path $site 'downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null
foreach ($relative in @("installer\MemoryGuardian-$version-Setup.exe")) {
    $source = Join-Path (Join-Path $root 'artifacts') $relative
    if (-not (Test-Path -LiteralPath $source)) { throw "Build and package the application first: $source" }
    Copy-Item -LiteralPath $source -Destination $downloads -Force
}
$published = @("MemoryGuardian-$version-Setup.exe")
foreach ($file in Get-ChildItem -LiteralPath $downloads -File) {
    if ($published -notcontains $file.Name) { throw "Remove obsolete download before packaging: $($file.FullName)" }
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$output = Join-Path $root "artifacts\MemoryGuardian-$version-website.zip"
$stream = [IO.File]::Open($output, [IO.FileMode]::Create)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $site -Recurse -File) {
        $relative = $file.FullName.Substring($site.Length).TrimStart('\').Replace('\','/')
        if ($relative.StartsWith('downloads/') -and $published -notcontains $file.Name) { continue }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,'memory-guardian/' + $relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Write-Output "Upload package: $output"
