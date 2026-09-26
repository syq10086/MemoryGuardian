param([string]$Dotnet = 'dotnet', [switch]$Preview)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'orb\MemoryOrb.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$output = Join-Path $root $(if ($Preview) { "artifacts\earth-preview-$version" } else { "artifacts\earth-release-$version" })
$argsList = @('build', (Join-Path $root 'orb\MemoryOrb.csproj'), '-c', 'Release', '-o', $output)
if ($Preview) { $argsList += '-p:OrbPreview=true' }
& $Dotnet @argsList
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output
Write-Output "Built: $output\EarthGuardian.exe"
