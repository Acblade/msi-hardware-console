$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifacts = Join-Path $root 'artifacts'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

& $csc /nologo /target:exe /platform:x64 /optimize+ /reference:System.Management.dll `
    ('/out:' + (Join-Path $artifacts 'SmokeTests.exe')) `
    (Join-Path $root 'tests\SmokeTests.cs') `
    (Join-Path $root 'src\Compatibility.cs') `
    (Join-Path $root 'src\Localization.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $artifacts 'SmokeTests.exe')
exit $LASTEXITCODE
