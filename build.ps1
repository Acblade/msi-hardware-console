param(
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src'
$assets = Join-Path $root 'assets'
$dist = Join-Path $root 'dist'
$artifacts = Join-Path $root 'artifacts'
$qa = Join-Path $artifacts 'qa'
$distDocs = Join-Path $dist 'docs'
$distImages = Join-Path $distDocs 'images'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
    throw '.NET Framework 4.x x64 compiler was not found.'
}

New-Item -ItemType Directory -Path $dist, $artifacts, $qa, $distDocs, $distImages -Force | Out-Null

$wpfReferences = 'PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml' | ForEach-Object {
    '/reference:' + [Reflection.Assembly]::LoadWithPartialName($_).Location
}
$sources = 'Program.cs', 'MainWindow.cs', 'Hardware.cs', 'Infrastructure.cs', 'Metrics.cs', 'CurveUI.cs', 'Localization.cs', 'Compatibility.cs' | ForEach-Object {
    Join-Path $source $_
}
$common = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warn:4',
    ('/win32icon:' + (Join-Path $assets 'MSIHardwareConsole.ico'))
) + $wpfReferences + $sources

& $csc (@(
    ('/win32manifest:' + (Join-Path $source 'app.manifest')),
    ('/out:' + (Join-Path $dist 'MSIHardwareConsole.exe'))
) + $common)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $csc (@(
    ('/win32manifest:' + (Join-Path $source 'app.qa.manifest')),
    ('/out:' + (Join-Path $qa 'MSIHardwareConsole-QA.exe'))
) + $common)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

'MSIHardwareConsole.png', 'MSIHardwareConsole-header.png', 'MSIHardwareConsole.ico' | ForEach-Object {
    Copy-Item -LiteralPath (Join-Path $assets $_) -Destination (Join-Path $dist $_) -Force
    Copy-Item -LiteralPath (Join-Path $assets $_) -Destination (Join-Path $qa $_) -Force
}
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $dist 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.zh-CN.md') -Destination (Join-Path $dist 'README.zh-CN.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $dist 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $root 'SECURITY.md') -Destination (Join-Path $dist 'SECURITY.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\COMPATIBILITY.md') -Destination (Join-Path $distDocs 'COMPATIBILITY.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\images\dashboard-en.png') -Destination (Join-Path $distImages 'dashboard-en.png') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\images\dashboard-zh-CN.png') -Destination (Join-Path $distImages 'dashboard-zh-CN.png') -Force

if (-not $SkipPackage) {
    $zip = Join-Path $artifacts 'MSI-Hardware-Console-v0.1.0-win-x64.zip'
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Output $zip
}

Write-Output (Join-Path $dist 'MSIHardwareConsole.exe')
