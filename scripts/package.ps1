param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -Dotnet $Dotnet
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Zip([string]$Target, [string]$Base, [System.IO.FileInfo[]]$Files, [string]$Prefix) {
    $stream = [IO.File]::Open($Target, [IO.FileMode]::Create)
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $Files) {
            $relative = $file.FullName.Substring($Base.Length).TrimStart('\').Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $Prefix + $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
}

$output = Join-Path $root 'artifacts'
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'orb\MemoryOrb.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$release = Join-Path $output "earth-release-$version"
$binaryFiles = Get-ChildItem -LiteralPath $release -File | Where-Object { $_.Extension -ne '.pdb' }
Write-Zip (Join-Path $output "MemoryGuardian-$version-windows.zip") $release $binaryFiles 'MemoryGuardian/'
$sourceFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object {
    $_.FullName.Substring($root.Length) -notmatch '[\\/](artifacts|bin|obj|packages|tmp|downloads|\.git|\.vs)[\\/]'
}
Write-Zip (Join-Path $output "MemoryGuardian-$version-source.zip") $root $sourceFiles 'MemoryGuardian/'
Get-ChildItem -LiteralPath $output -Filter "MemoryGuardian-$version-*.zip" | Get-FileHash -Algorithm SHA256 |
    ForEach-Object { $_.Hash + '  ' + [IO.Path]::GetFileName($_.Path) } |
    Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Packages: $output"
