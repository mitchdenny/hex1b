param(
    [string]$Configuration = "Debug",
    [string]$Rid = "win-x64",
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

    throw "Unable to locate $Name. Install Rust and ensure it is available on PATH."
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $repoRoot "src\Hex1b.PtyHost.Rust\Cargo.toml"
$crateDir = Split-Path $manifestPath -Parent
$managedHostProject = Join-Path $repoRoot "src\Hex1b.PtyHost\Hex1b.PtyHost.csproj"

if (-not (Test-Path $manifestPath)) {
    throw "The Rust PTY host manifest was not found at $manifestPath."
}

$targetTriple = switch ($Rid) {
    "win-x64" { "x86_64-pc-windows-msvc" }
    "win-arm64" { "aarch64-pc-windows-msvc" }
    default { throw "Unsupported Windows PTY shim RID '$Rid'." }
}

$sdkArchFolder = switch ($Rid) {
    "win-x64" { "x86_64" }
    "win-arm64" { "aarch64" }
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "src\Hex1b\obj\windows-pty-shim\$Rid\native"
}

$cargo = Resolve-ToolPath -Name "cargo.exe" -Fallback (Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe")
$rustup = Resolve-ToolPath -Name "rustup.exe" -Fallback (Join-Path $env:USERPROFILE ".cargo\bin\rustup.exe")
$dotnet = Resolve-ToolPath -Name "dotnet.exe" -Fallback (Join-Path ${env:ProgramFiles} "dotnet\dotnet.exe")
$rustc = (& $rustup which rustc).Trim()
$rustSysroot = Split-Path (Split-Path $rustc -Parent) -Parent

$originalLib = $env:LIB
$originalRustFlags = $env:RUSTFLAGS
$targetLinkerEnvName = "CARGO_TARGET_{0}_LINKER" -f ($targetTriple.ToUpperInvariant().Replace('-', '_'))
$originalTargetLinker = [Environment]::GetEnvironmentVariable($targetLinkerEnvName)

try {
    & $rustup target add $targetTriple | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install the Rust target '$targetTriple'."
    }

    $existingLibPaths = @()
    if ($env:LIB) {
        $existingLibPaths = $env:LIB -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $linkerAvailable = $null -ne (Get-Command "link.exe" -ErrorAction SilentlyContinue)
    $hasKernel32Lib = Test-LibSearchPath -Paths $existingLibPaths

    if (-not $linkerAvailable -or -not $hasKernel32Lib) {
        $xwinRootCandidates = @(
            (Join-Path $env:TEMP "hex1b-xwin\splat"),
            (Join-Path $env:LOCALAPPDATA "Temp\hex1b-xwin\splat")
        ) | Where-Object { $_ -and (Test-Path $_) }

        $xwinRoot = $xwinRootCandidates | Select-Object -First 1
        if (-not $xwinRoot) {
            throw @"
No Windows SDK import libraries were found for the Rust PTY host build.

Install the Visual Studio C++ build tools, or populate the xwin sysroot at:
  $env:TEMP\hex1b-xwin\splat
"@
        }

        $xwinLibs = @(
            (Join-Path $xwinRoot "crt\lib\$sdkArchFolder"),
            (Join-Path $xwinRoot "sdk\lib\ucrt\$sdkArchFolder"),
            (Join-Path $xwinRoot "sdk\lib\um\$sdkArchFolder")
        ) | Where-Object { Test-Path $_ }

        if ($xwinLibs.Count -lt 3) {
            throw "The xwin sysroot at '$xwinRoot' is incomplete for architecture '$sdkArchFolder'."
        }

        $rustLld = Get-ChildItem -Path $rustSysroot -Recurse -Filter "rust-lld.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ([string]::IsNullOrWhiteSpace($rustLld) -or -not (Test-Path $rustLld)) {
            throw "Unable to locate rust-lld.exe in the installed Rust toolchain."
        }

        $env:LIB = ($xwinLibs + $existingLibPaths) -join ";"
        [Environment]::SetEnvironmentVariable($targetLinkerEnvName, $rustLld)
        $env:RUSTFLAGS = if ([string]::IsNullOrWhiteSpace($originalRustFlags)) {
            "-C linker=$rustLld"
        }
        else {
            "-C linker=$rustLld $originalRustFlags"
        }
    }

    $cargoArgs = @(
        "build",
        "--manifest-path", $manifestPath,
        "--target", $targetTriple
    )

    if ($Configuration -eq "Release") {
        $cargoArgs += "--release"
    }

    Push-Location $crateDir
    try {
        & $cargo @cargoArgs
        if ($LASTEXITCODE -ne 0) {
            throw "cargo build failed for the Windows PTY shim."
        }
    }
    finally {
        Pop-Location
    }

    $cargoProfile = if ($Configuration -eq "Release") { "release" } else { "debug" }
    $builtBinary = Join-Path $crateDir "target\$targetTriple\$cargoProfile\hex1bpty.exe"
    if (-not (Test-Path $builtBinary)) {
        throw "Expected PTY shim binary was not produced at '$builtBinary'."
    }

    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    Copy-Item $builtBinary (Join-Path $OutputDir "hex1bpty.exe") -Force
    Write-Host "Built Windows PTY shim: $(Join-Path $OutputDir 'hex1bpty.exe')"

    if (-not (Test-Path $managedHostProject)) {
        throw "The managed PTY host project was not found at $managedHostProject."
    }

    $managedPublishDir = Join-Path $repoRoot "artifacts\obj\hex1bpty-managed\$Rid\$Configuration"
    if (Test-Path $managedPublishDir) {
        Remove-Item -Path $managedPublishDir -Recurse -Force
    }

    $managedPublishArgs = @(
        "publish", $managedHostProject,
        "-c", $Configuration,
        "-r", $Rid,
        "-o", $managedPublishDir,
        "-p:PublishSingleFile=true",
        "--nologo"
    )

    if ($linkerAvailable -and $hasKernel32Lib) {
        $managedPublishArgs += "-p:PublishAot=true"
        $managedPublishArgs += "-p:SelfContained=true"
    }
    else {
        $managedPublishArgs += "-p:PublishAot=false"
        $managedPublishArgs += "-p:SelfContained=false"
        Write-Warning "Publishing the managed Windows PTY shim as a framework-dependent single-file app because Native AOT linker prerequisites are unavailable on this machine."
    }

    & $dotnet @managedPublishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for the managed Windows PTY shim."
    }

    $managedBinary = Join-Path $managedPublishDir "hex1bpty.exe"
    if (-not (Test-Path $managedBinary)) {
        throw "Expected managed PTY shim binary was not produced at '$managedBinary'."
    }

    Copy-Item $managedBinary (Join-Path $OutputDir "hex1bpty-managed.exe") -Force
    Write-Host "Built managed Windows PTY shim: $(Join-Path $OutputDir 'hex1bpty-managed.exe')"
}
finally {
    $env:LIB = $originalLib
    $env:RUSTFLAGS = $originalRustFlags
    [Environment]::SetEnvironmentVariable($targetLinkerEnvName, $originalTargetLinker)
}
