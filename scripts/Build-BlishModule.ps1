param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $Install,

    [string] $ModulesPath = "$env:USERPROFILE\Documents\Guild Wars 2\addons\blishhud\modules"
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "src\Gw2FlipOverlay\Gw2FlipOverlay.csproj"

function Find-MSBuild {
    $candidates = @()

    if (Get-Command msbuild.exe -ErrorAction SilentlyContinue) {
        $candidates += (Get-Command msbuild.exe).Source
    }

    $vsWherePaths = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe",
        "C:\Program Files\Microsoft Visual Studio\Installer\vswhere.exe"
    )

    foreach ($vsWhere in $vsWherePaths) {
        if (Test-Path $vsWhere) {
            $installationPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -property installationPath
            if ($LASTEXITCODE -eq 0 -and $installationPath) {
                $candidate = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
                if (Test-Path $candidate) {
                    $candidates += $candidate
                }
            }
        }
    }

    $fallbacks = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($fallback in $fallbacks) {
        if (Test-Path $fallback) {
            $candidates += $fallback
        }
    }

    return $candidates | Select-Object -First 1
}

$msbuildPath = Find-MSBuild

if (-not $msbuildPath) {
    throw "MSBuild was not found. Install Visual Studio or Build Tools with .NET Framework 4.7.2 support first."
}

Write-Host "Using MSBuild:" $msbuildPath
Write-Host "Building project:" $projectPath

& $msbuildPath $projectPath /restore /t:Build /p:Configuration=$Configuration /nologo /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$packagePath = Join-Path $workspaceRoot "src\Gw2FlipOverlay\bin\$Configuration\Gw2FlipOverlay.bhm"

if (-not (Test-Path $packagePath)) {
    throw "The .bhm package was not created at expected path: $packagePath"
}

Write-Host ""
Write-Host "Created package:" $packagePath

if ($Install) {
    if (-not (Test-Path $ModulesPath)) {
        throw "Blish HUD modules folder was not found: $ModulesPath"
    }

    $destinationPath = Join-Path $ModulesPath "Gw2FlipOverlay.bhm"
    try {
        Copy-Item -Path $packagePath -Destination $destinationPath -Force
        Write-Host "Installed package to:" $destinationPath
    } catch {
        Write-Host ""
        Write-Warning "Could not copy the module because Blish HUD is still using the file."
        Write-Host "Close Blish HUD fully, then run this command again:"
        Write-Host "powershell -ExecutionPolicy Bypass -File .\scripts\Build-BlishModule.ps1 -Install"
        Write-Host ""
        Write-Host "Or copy it manually from:"
        Write-Host $packagePath
        Write-Host "To:"
        Write-Host $destinationPath
        throw
    }
}
