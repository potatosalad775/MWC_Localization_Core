#Requires -Version 5.0
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$GameDir,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$MSBuildArgs
)

$ErrorActionPreference = 'Stop'

# My Summer Car Steam App ID
$MscAppId = '516750'

function Get-SteamLibraryRoots {
    $steamRoot = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' `
        -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if (-not $steamRoot) { return @() }
    $steamRoot = $steamRoot -replace '/', '\'

    $roots = [System.Collections.Generic.List[string]]::new()
    $roots.Add($steamRoot)

    $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        $content = Get-Content -Raw -LiteralPath $vdf
        foreach ($m in [regex]::Matches($content, '"path"\s+"([^"]+)"')) {
            $roots.Add(($m.Groups[1].Value -replace '\\\\', '\'))
        }
    }
    return $roots
}

function Find-MscGameDir {
    foreach ($lib in Get-SteamLibraryRoots) {
        $manifest = Join-Path $lib "steamapps\appmanifest_$MscAppId.acf"
        if (-not (Test-Path $manifest)) { continue }

        $m = [regex]::Match((Get-Content -Raw -LiteralPath $manifest), '"installdir"\s+"([^"]+)"')
        if (-not $m.Success) { continue }

        $candidate = Join-Path $lib "steamapps\common\$($m.Groups[1].Value)\mysummercar_Data\Managed"
        if (Test-Path (Join-Path $candidate 'Assembly-CSharp.dll')) {
            return $candidate
        }
    }
    return $null
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at '$vswhere'. Install Visual Studio 2017+ or VS Build Tools."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
    -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild.exe not found. Install the 'MSBuild' component via Visual Studio Installer."
}

$properties = @("-p:Configuration=$Configuration")

if (-not $GameDir) {
    $detected = Find-MscGameDir
    if ($detected) {
        Write-Host "GameDir (detected): $detected"
        $GameDir = $detected
    } else {
        Write-Host "GameDir not detected via Steam libraries; Directory.Build.props fallbacks will be tried."
    }
} else {
    Write-Host "GameDir (override): $GameDir"
}
if ($GameDir) {
    $properties += "-p:GameDir=$GameDir"
}

$project = Join-Path $PSScriptRoot 'MSC_Localization_Core.csproj'
& $msbuild $project @properties @MSBuildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$builtDll = Join-Path $PSScriptRoot "bin\$Configuration\MWC_Localization_Core.dll"
$distDir = Join-Path $PSScriptRoot 'dist'
if (Test-Path -LiteralPath $builtDll) {
    Copy-Item -LiteralPath $builtDll -Destination $distDir -Force
    Write-Host "Copied DLL into $distDir"
} else {
    Write-Warning "Built DLL not found at '$builtDll'; skipping dist copy."
}

exit 0
