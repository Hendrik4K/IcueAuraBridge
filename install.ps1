# Installs IcueAuraBridge system-wide:
#  - copies the DLL to C:\Program Files\IcueAuraBridge (admin-only, because iCUE's
#    plugin host loads it into a SYSTEM process)
#  - registers it as the in-process COM server "aura.sdk.1" that iCUE's ASUS plugin uses
#  - restarts iCUE's plugin host and iCUE so the ASUS plugin enumerates again
# Use -Uninstall to remove it again. Asks for admin rights when needed.
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$log = Join-Path $env:TEMP 'IcueAuraBridge-install.log'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $shell = (Get-Process -Id $PID).Path
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($Uninstall) { $arguments += '-Uninstall' }
    Start-Process $shell -Verb RunAs -Wait -ArgumentList $arguments
    if (Test-Path $log) {
        Get-Content $log | Where-Object { $_ -match '^(IcueAuraBridge|  |ERROR)' }
    }
    return
}

Start-Transcript -Path $log -Force | Out-Null
try {
    $clsid = '{05921124-5057-483E-A037-E9497B523590}'
    $progId = 'aura.sdk.1'
    $target = Join-Path $env:ProgramFiles 'IcueAuraBridge'
    $dll = Join-Path $target 'IcueAuraBridge.dll'
    $data = Join-Path $env:ProgramData 'IcueAuraBridge'
    $classes = 'HKLM:\SOFTWARE\Classes'
    $source = Join-Path $PSScriptRoot 'bin\IcueAuraBridge.dll'

    # Find iCUE through its plugin host service.
    $svc = Get-CimInstance Win32_Service -Filter "Name='iCUEDevicePluginHost'"
    if (-not $svc) { throw 'iCUE 4 does not seem to be installed (service iCUEDevicePluginHost not found).' }
    $icueDir = Split-Path ($svc.PathName.Trim('"') -replace '"\s.*$', '')
    $icueExe = Join-Path $icueDir 'iCUE.exe'
    if (-not (Test-Path (Join-Path $icueDir 'plugins\ASUS\asus_plugin.dll'))) {
        throw "iCUE's ASUS plugin was not found in $icueDir\plugins\ASUS."
    }

    if (Test-Path "$classes\CLSID\$clsid\InprocServer32") {
        $existing = Get-ItemProperty "$classes\CLSID\$clsid\InprocServer32"
        if ($existing.Class -ne 'IcueAuraBridge.AuraSdk') {
            throw "aura.sdk.1 is already registered by other software (probably Armoury Crate / ASUS LightingService). Uninstall that first."
        }
    }
    if (-not $Uninstall -and -not (Test-Path $source)) {
        throw "$source not found. Download a release or run build.ps1 first."
    }

    'IcueAuraBridge: stopping iCUE and its plugin host'
    Get-Process iCUE -ErrorAction SilentlyContinue | Stop-Process -Force
    Stop-Service iCUEDevicePluginHost -Force -ErrorAction SilentlyContinue
    Get-Process iCUEDevicePluginHost -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    if ($Uninstall) {
        Remove-Item -Recurse -Force "$classes\CLSID\$clsid", "$classes\$progId" -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force $target -ErrorAction SilentlyContinue
        'IcueAuraBridge: removed. Log and settings stay in ' + $data
    }
    else {
        New-Item -ItemType Directory -Force $target | Out-Null
        Copy-Item $source $dll -Force
        Unblock-File $dll   # .NET refuses to load DLLs that carry the "downloaded from the internet" mark

        $asm = [Reflection.AssemblyName]::GetAssemblyName($dll)
        $codeBase = 'file:///' + $dll.Replace('\', '/')
        New-Item -Force "$classes\$progId\CLSID" -Value $clsid | Out-Null
        Set-Item "$classes\$progId" -Value 'IcueAuraBridge.AuraSdk'
        New-Item -Force "$classes\CLSID\$clsid" -Value 'IcueAuraBridge.AuraSdk' | Out-Null
        New-Item -Force "$classes\CLSID\$clsid\ProgId" -Value $progId | Out-Null
        Remove-Item -Recurse -Force "$classes\CLSID\$clsid\InprocServer32" -ErrorAction SilentlyContinue
        foreach ($k in "$classes\CLSID\$clsid\InprocServer32", "$classes\CLSID\$clsid\InprocServer32\$($asm.Version)") {
            New-Item -Force $k | Out-Null
            if ($k.EndsWith('InprocServer32')) {
                Set-Item $k -Value 'mscoree.dll'
                Set-ItemProperty $k ThreadingModel 'Both'
            }
            Set-ItemProperty $k Class 'IcueAuraBridge.AuraSdk'
            Set-ItemProperty $k Assembly $asm.FullName
            Set-ItemProperty $k RuntimeVersion 'v4.0.30319'
            Set-ItemProperty $k CodeBase $codeBase
        }

        New-Item -ItemType Directory -Force $data | Out-Null
        $cfg = Join-Path $data 'config.ini'
        if (-not (Test-Path $cfg)) {
            @(
                '# IcueAuraBridge settings. Restart iCUE after changes.'
                'motherboard = true'
                'gpu = true'
                '# LEDs connected to the mainboard ARGB headers (0 = header not shown in iCUE)'
                'argb_header_1_leds = 0'
                '# Also try ASUS graphics cards that are not on the known list (at your own risk)'
                'gpu_allow_unlisted = false'
            ) | Set-Content $cfg -Encoding utf8
        }
        "IcueAuraBridge: installed $($asm.Version) to $dll"
        "IcueAuraBridge: log and settings in $data"
    }

    'IcueAuraBridge: starting iCUE again'
    Start-Service iCUEDevicePluginHost
    if (Test-Path $icueExe) {
        # Start iCUE un-elevated as the logged-on user via Explorer.
        Start-Process explorer.exe -ArgumentList "`"$icueExe`""
    }
}
catch {
    "ERROR: $($_.Exception.Message)"
    throw
}
finally {
    Stop-Transcript | Out-Null
}
