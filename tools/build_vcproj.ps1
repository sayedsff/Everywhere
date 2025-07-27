param(
    [string]$projectPath,
    [string]$configuration
)

function Find-VSWhere {
    $drives = Get-PSDrive -PSProvider FileSystem
    $programFiles = "Program Files", "Program Files (x86)"
    $subPath = "Microsoft Visual Studio\Installer\vswhere.exe"

    foreach ($drive in $drives) {
        foreach ($programFile in $programFiles) {
            $possiblePath = Join-Path $drive.Root $programFile
            $possiblePath = Join-Path $possiblePath $subPath
            if (Test-Path $possiblePath) {
                return $possiblePath
            }
        }
    }

    # 如果在盘符路径中未找到，尝试从 PATH 环境变量中找到
    $pathFromEnv = Get-Command vswhere.exe -ErrorAction SilentlyContinue
    if ($pathFromEnv) {
        return $pathFromEnv.Source
    }

    return $null
}

function Find-LatestMSBuild {
    $vsWherePath = Find-VSWhere
    if (-not $vsWherePath) {
        Write-Host "vswhere not found. Please make sure Visual Studio is installed."
        return $null
    }

    $vsPath = & $vsWherePath -latest -prerelease -products * -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VC.* -property installationPath
    if (-not $vsPath) {
        Write-Host "Visual Studio installation not found."
        return $null
    }

    $msBuildPath = Join-Path $vsPath "MSBuild\Current\Bin\amd64\MSBuild.exe"
    if (Test-Path $msBuildPath) {
        return $msBuildPath
    }

    $msBuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path $msBuildPath)) {
        Write-Host "MSBuild not found in the latest Visual Studio installation."
        return $null
    }

    return $msbuildPath
}

$msbuildPath = Find-LatestMSBuild
if (-not $msbuildPath) {
    Write-Host "MSBuild not found. Please make sure it is installed and added to the system PATH."
    exit -1
}
Write-Host "MSBuild found at $msbuildPath"

$scriptDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
if (-not (Test-Path $projectPath)) {
    Write-Host "$projectPath not found."
    exit -2
}

$msbuildArgs = @(
    "`"$projectPath`"",
    "/p:Platform=x64",
    "/p:Configuration=$configuration"
)

Write-Host "Building $projectPath for $configuration..."
& $msbuildPath $msbuildArgs