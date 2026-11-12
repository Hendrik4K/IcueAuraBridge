# Builds bin\IcueAuraBridge.dll with the C# compiler that ships with .NET Framework 4.x
# (part of every Windows 10/11 installation, no Visual Studio needed).
# -Package additionally creates dist\IcueAuraBridge-<version>.zip for a release.
param([switch]$Package)

$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

& $csc /nologo /target:library /platform:anycpu /optimize+ /warn:4 `
    "/out:$out\IcueAuraBridge.dll" (Join-Path $PSScriptRoot 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "Built $out\IcueAuraBridge.dll"

if ($Package) {
    $version = [Reflection.AssemblyName]::GetAssemblyName("$out\IcueAuraBridge.dll").Version.ToString(3)
    $stage = Join-Path $PSScriptRoot "dist\IcueAuraBridge-$version"
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$stage\bin" | Out-Null
    Copy-Item "$out\IcueAuraBridge.dll" "$stage\bin\"
    Copy-Item (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'LICENSE') $stage
    $zip = "$stage.zip"
    Remove-Item $zip -ErrorAction SilentlyContinue
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    Write-Host "Packaged $zip"
}
