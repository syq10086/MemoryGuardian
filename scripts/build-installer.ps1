param([string]$Dotnet = 'dotnet', [string]$Iscc = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Iscc) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Iscc = $command.Source }
    else {
        foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
            if (Test-Path -LiteralPath $candidate) { $Iscc = $candidate; break }
        }
    }
}
if (-not $Iscc -or -not (Test-Path -LiteralPath $Iscc)) { throw 'Install Inno Setup 6 or pass -Iscc with the full path to ISCC.exe.' }
& (Join-Path $PSScriptRoot 'build.ps1') -Dotnet $Dotnet
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'orb\MemoryOrb.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$version = @($version | Where-Object { $_ })[0]
$output = Join-Path $root 'artifacts\installer'
$source = Join-Path $root "artifacts\earth-release-$version"
& $Iscc "/DAppVersion=$version" "/DSourceDir=$source" "/DOutputDir=$output" (Join-Path $root 'installer\EarthGuardian.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $output "MemoryGuardian-$version-Setup.exe"
$hash = Get-FileHash -LiteralPath $setup -Algorithm SHA256
($hash.Hash + '  ' + [IO.Path]::GetFileName($setup)) | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Installer: $setup"
