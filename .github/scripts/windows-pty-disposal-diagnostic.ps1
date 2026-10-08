param(
    [Parameter(Mandatory)][string]$TestExecutable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$ProcDump,
    [Parameter(Mandatory)][string]$DotnetDump,
    [ValidateRange(1, 100)][int]$Repetitions = 10,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 120,
    [switch]$VerifyWatchdog
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This diagnostic requires Windows.' }
$TestExecutable = (Resolve-Path $TestExecutable).Path
$ProcDump = (Resolve-Path $ProcDump).Path
$DotnetDump = (Resolve-Path $DotnetDump).Path
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
$filter = 'FullyQualifiedName=Hex1b.Tests.WindowsPtyDisposeTests.DisposeAsync_OptInDiagnostic_RecordsLifecycle'

function Invoke-BoundedTool($executable, $arguments, $logPath) {
    $tool = Start-Process $executable -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput "$logPath.stdout.txt" -RedirectStandardError "$logPath.stderr.txt"
    try {
        if (-not $tool.WaitForExit(60000)) {
            Stop-Process -Id $tool.Id -Force
            throw "Diagnostic tool $executable timed out; see $logPath."
        }
        if ($tool.ExitCode -ne 0) { throw "Diagnostic tool $executable exited $($tool.ExitCode); see $logPath." }
    }
    finally { $tool.Dispose() }
}

for ($attempt = 1; $attempt -le $Repetitions; $attempt++) {
    $directory = Join-Path $OutputDirectory "attempt-$attempt"
    New-Item -ItemType Directory $directory | Out-Null
    $env:HEX1B_PTY_DISPOSE_DIAGNOSTIC = '1'
    $env:HEX1B_PTY_DISPOSE_WATCHDOG_SELF_TEST = if ($VerifyWatchdog) { '1' } else { '0' }
    $env:HEX1B_PTY_DISPOSE_LIFECYCLE_FILE = Join-Path $directory lifecycle.log
    $env:HEX1B_PTY_SHIM_LOGFILE = Join-Path $directory helper.log
    $env:HEX1B_PTY_SHIM_CLIENT_TRACE_FILE = Join-Path $directory client.log
    $env:HEX1B_PTY_SHIM_PROTOCOL_TRACE_FILE = Join-Path $directory protocol.log
    $known = @{}
    $process = Start-Process $TestExecutable -PassThru -ArgumentList @(
        '--filter', $filter, '--no-progress', '--no-ansi',
        '--diagnostic', '--diagnostic-output-directory', "`"$directory`""
    ) -RedirectStandardOutput "$directory/test.stdout.txt" -RedirectStandardError "$directory/test.stderr.txt"
    $timer = [Diagnostics.Stopwatch]::StartNew()

    try {
        do {
            $snapshot = @(Get-CimInstance Win32_Process)
            $root = $snapshot | Where-Object ProcessId -EQ $process.Id
            if ($root) { $known[[int]$root.ProcessId] = $root }
            do {
                $added = $false
                foreach ($child in $snapshot) {
                    if ($known.ContainsKey([int]$child.ParentProcessId) -and
                        -not $known.ContainsKey([int]$child.ProcessId)) {
                        $parent = $snapshot | Where-Object ProcessId -EQ $child.ParentProcessId
                        if (-not $parent -or $parent.CreationDate -ne $known[[int]$child.ParentProcessId].CreationDate -or
                            $child.CreationDate -lt $parent.CreationDate) { continue }
                        $known[[int]$child.ProcessId] = $child
                        $added = $true
                    }
                }
            } while ($added)
            $known.Values | Select-Object ProcessId, ParentProcessId, Name, CreationDate |
                ConvertTo-Json | Set-Content "$directory/process-tree.json"
        } while (-not $process.WaitForExit(1000) -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds)

        if (-not $process.HasExited) {
            "Watchdog expired after $TimeoutSeconds seconds." | Set-Content "$directory/watchdog.txt"
            $dumps = @()
            foreach ($target in $known.Values | Sort-Object @{ Expression = { $_.ProcessId -ne $process.Id } },
                @{ Expression = { $_.Name -ne 'hex1bpty.exe' } }, ProcessId) {
                $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($target.ProcessId)"
                if (-not $current -or $current.CreationDate -ne $target.CreationDate) { continue }
                $dump = Join-Path $directory "$($target.Name)-$($target.ProcessId).dmp"
                try {
                    Invoke-BoundedTool $ProcDump @('-accepteula', '-ma', "$($target.ProcessId)", "`"$dump`"") `
                        "$directory/dump-$($target.ProcessId)"
                    if ($target.Name -in @('Hex1b.Tests.exe', 'hex1bpty.exe')) {
                        $dumps += @{ Path = $dump; ProcessId = $target.ProcessId }
                    }
                }
                catch {
                    $_ | Out-String | Add-Content "$directory/capture-errors.txt"
                    Write-Warning $_
                }
            }
            foreach ($dump in $dumps) {
                try {
                    Invoke-BoundedTool $DotnetDump @('analyze', "`"$($dump.Path)`"", '-c', '"threads"', '-c', '"clrstack -all"', '-c', '"exit"') `
                        "$directory/stacks-$($dump.ProcessId)"
                }
                catch {
                    $_ | Out-String | Add-Content "$directory/capture-errors.txt"
                    Write-Warning $_
                }
            }
            if ($VerifyWatchdog) {
                $parentDump = $dumps | Where-Object ProcessId -EQ $process.Id
                $helperDumps = $dumps | Where-Object { (Split-Path $_.Path -Leaf) -like 'hex1bpty.exe-*' }
                if (-not $parentDump -or -not $helperDumps) {
                    throw 'Watchdog self-test did not capture both parent and helper dumps.'
                }
                foreach ($dump in @($parentDump) + @($helperDumps)) {
                    $stackLog = "$directory/stacks-$($dump.ProcessId).stdout.txt"
                    if (-not (Test-Path $stackLog) -or (Get-Item $stackLog).Length -eq 0) {
                        throw "Watchdog self-test is missing managed stack output: $stackLog"
                    }
                }
                if (Test-Path "$directory/capture-errors.txt") {
                    throw 'Watchdog self-test had capture errors; see capture-errors.txt.'
                }
                Write-Host 'Watchdog self-test captured parent/helper dumps and managed stacks.'
                return
            }
            throw "PTY diagnostic timed out on attempt $attempt. Dumps and lifecycle logs: $directory"
        }
        if ($process.ExitCode -ne 0) { throw "Probe exited $($process.ExitCode) on attempt $attempt; see $directory." }
        $lifecycle = Get-Content $env:HEX1B_PTY_DISPOSE_LIFECYCLE_FILE -Raw
        if ($lifecycle -notmatch 'diagnostic.end') { throw "Diagnostic entry point did not complete on attempt $attempt." }
        Write-Host "Attempt $attempt completed."
    }
    finally {
        # Recheck creation time to avoid terminating a reused PID. Never kill by name.
        foreach ($target in $known.Values | Sort-Object ProcessId -Descending) {
            $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($target.ProcessId)"
            if ($current -and $current.CreationDate -eq $target.CreationDate) {
                Stop-Process -Id $target.ProcessId -Force -ErrorAction Continue
            }
        }
        if (-not $process.HasExited) { $process.Kill() }
        $process.Dispose()
    }
}
