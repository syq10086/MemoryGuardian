param([string]$Setup = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Setup) { $Setup = Join-Path $root 'artifacts\installer\MemoryGuardian-0.6.1-Setup.exe' }
$target = [IO.Path]::GetFullPath((Join-Path $root 'tmp\installer-smoke'))
$expected = [IO.Path]::GetFullPath((Join-Path $root 'tmp\installer-smoke'))
if ($target -ne $expected -or (Test-Path -LiteralPath $target)) { throw 'Smoke target must be a fresh isolated directory.' }
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) '内存卫士.lnk'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) '内存卫士'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{92B673D8-984D-49F8-AF90-31A304ED992A}_is1'
foreach ($existing in @($shortcut,$menu,$key)) {
    if (Test-Path -LiteralPath $existing) { throw "Existing user installation must not be overwritten: $existing" }
}
$log = Join-Path $root 'tmp\installer-smoke-install.log'
$arguments = "/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOCLOSEAPPLICATIONS /DIR=`"$target`" /LOG=`"$log`""
$process = Start-Process -FilePath $Setup -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Install failed: $($process.ExitCode). See $log" }
try {
    foreach ($file in @('EarthGuardian.exe','EarthGuardian.exe.config','LICENSE','NOTICE.md','README.md','unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $target $file))) { throw "Missing installed file: $file" }
    }
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Desktop shortcut not created by default.' }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if ($link.TargetPath -ne (Join-Path $target 'EarthGuardian.exe')) { throw 'Incorrect desktop shortcut target.' }
    if (-not (Test-Path -LiteralPath (Join-Path $menu '内存卫士.lnk'))) { throw 'Start menu shortcut missing.' }
    $registration = Get-ItemProperty -LiteralPath $key
    if ($registration.DisplayName -notlike '*内存卫士*') { throw 'Uninstall registration missing.' }
    Write-Output 'PASS: install files, default desktop shortcut, shortcut target, Start menu, uninstall registration'
} finally {
    # Run only the uninstaller produced inside this verified disposable target.
    $uninstaller = Join-Path $target 'unins000.exe'
    if ((Split-Path -Parent $uninstaller) -ne $expected) { throw 'Unsafe uninstall target.' }
    $uninstallLog = Join-Path $root 'tmp\installer-smoke-uninstall.log'
    $process = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$uninstallLog`"" -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
}
foreach ($remaining in @($shortcut,$key,(Join-Path $target 'EarthGuardian.exe'))) {
    if (Test-Path -LiteralPath $remaining) { throw "Uninstall left behind: $remaining" }
}
Write-Output 'PASS: uninstall removed application, desktop shortcut and registration'
Write-Output 'Per-user install tested. The normal administrator/UAC path was not automated.'
