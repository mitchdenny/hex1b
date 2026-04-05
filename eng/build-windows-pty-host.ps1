param(
    [string]$Configuration = "Debug",
    [string]$Rid = "",
    [ValidateSet("Debug", "Aot")]
    [string]$PublishMode = "Debug",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

function Resolve-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$Fallback
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if ($Fallback -and (Test-Path $Fallback)) {
        return $Fallback
    }

    throw "Unable to locate $Name."
}

function Test-LibSearchPath {
    param([string[]]$Paths)

    foreach ($path in $Paths) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        if (Test-Path (Join-Path $path "kernel32.lib")) {
            return $true
        }
    }

    return $false
}

function Get-LibSearchPaths {
    $existingLibPaths = @()
    if ($env:LIB) {
        $existingLibPaths = $env:LIB -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    return $existingLibPaths
}

function Import-VsBuildEnvironment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Rid
    )

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        return $false
    }

    $requiredComponent = if ($Rid -eq "win-arm64") {
        "Microsoft.VisualStudio.Component.VC.Tools.ARM64"
    }
    else {
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64"
    }

    $installationPath = (& $vswhere -latest -products * -requires $requiredComponent -property installationPath | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($installationPath)) {
        $installationPath = (& $vswhere -latest -products * -property installationPath | Select-Object -First 1).Trim()
    }

    if ([string]::IsNullOrWhiteSpace($installationPath)) {
        return $false
    }

    $vsDevCmd = Join-Path $installationPath "Common7\Tools\VsDevCmd.bat"
    if (-not (Test-Path $vsDevCmd)) {
        return $false
    }

    $arch = if ($Rid -eq "win-arm64") { "arm64" } else { "x64" }
    $hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        "arm64"
    }
    else {
        "x64"
    }

    $environmentBlock = & cmd.exe /s /c "`"$vsDevCmd`" -no_logo -arch=$arch -host_arch=$hostArch >nul && set"
    if ($LASTEXITCODE -ne 0 -or -not $environmentBlock) {
        return $false
    }

    foreach ($line in $environmentBlock) {
        if ($line -notmatch "^(.*?)=(.*)$") {
            continue
        }

        $name = $matches[1]
        $value = $matches[2]
        Set-Item -Path "Env:$name" -Value $value
    }

    return $true
}

function Test-NativeAotPrerequisites {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Rid
    )

    $existingLibPaths = Get-LibSearchPaths
    $linkerAvailable = $null -ne (Get-Command "link.exe" -ErrorAction SilentlyContinue)
    $hasKernel32Lib = Test-LibSearchPath -Paths $existingLibPaths

    if ($linkerAvailable -and $hasKernel32Lib) {
        return $true
    }

    if (Import-VsBuildEnvironment -Rid $Rid) {
        $existingLibPaths = Get-LibSearchPaths
        $linkerAvailable = $null -ne (Get-Command "link.exe" -ErrorAction SilentlyContinue)
        $hasKernel32Lib = Test-LibSearchPath -Paths $existingLibPaths
        return $linkerAvailable -and $hasKernel32Lib
    }

    return $false
}

function Assert-NativeAotPrerequisites {
    $existingLibPaths = @()
    if (-not (Test-NativeAotPrerequisites -Rid $Rid)) {
        throw @"
Native AOT was requested for hex1bpty.exe, but the required Windows linker prerequisites were not found.

Install the Visual Studio Desktop Development for C++ workload (or equivalent Build Tools),
then re-run the build. For normal inner-loop debugging builds, use:
  -p:Hex1bPtyHostPublishMode=Debug
"@
    }
}

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    Write-Host "Skipping Windows PTY host build on a non-Windows machine."
    exit 0
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$managedHostProject = Join-Path $repoRoot "src\Hex1b.PtyHost\Hex1b.PtyHost.csproj"

if (-not (Test-Path $managedHostProject)) {
    throw "The managed PTY host project was not found at $managedHostProject."
}

if ([string]::IsNullOrWhiteSpace($Rid)) {
    $Rid = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        "win-arm64"
    }
    else {
        "win-x64"
    }
}

if ($Rid -notin @("win-x64", "win-arm64")) {
    throw "Unsupported Windows PTY shim RID '$Rid'."
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "src\Hex1b\obj\windows-pty-shim\$Rid\native"
}

$dotnet = Resolve-ToolPath -Name "dotnet.exe" -Fallback (Join-Path ${env:ProgramFiles} "dotnet\dotnet.exe")

if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if ($PublishMode -eq "Aot") {
    Assert-NativeAotPrerequisites -Rid $Rid

    & $dotnet publish $managedHostProject `
        -c $Configuration `
        -r $Rid `
        -o $OutputDir `
        -p:PublishAot=true `
        -p:SelfContained=true `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for the Native AOT Windows PTY host."
    }

    Write-Host "Built Windows PTY host (Native AOT): $(Join-Path $OutputDir 'hex1bpty.exe')"
    exit 0
}

& $dotnet build $managedHostProject `
    -c $Configuration `
    -r $Rid `
    --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed for the Windows PTY host."
}

$buildOutputDir = Join-Path $repoRoot "src\Hex1b.PtyHost\bin\$Configuration\net8.0\$Rid"
if (-not (Test-Path $buildOutputDir)) {
    throw "Expected PTY host build output was not produced at '$buildOutputDir'."
}

Copy-Item (Join-Path $buildOutputDir "*") $OutputDir -Recurse -Force
Write-Host "Built Windows PTY host (debuggable managed build): $(Join-Path $OutputDir 'hex1bpty.exe')"
