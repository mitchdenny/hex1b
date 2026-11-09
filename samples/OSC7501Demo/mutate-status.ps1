$ErrorActionPreference = 'Stop'

function Write-Osc7501([string] $Body) {
    [Console]::Write("$([char]27)]7501;$Body$([char]7)")
}

function Clear-Status {
    Write-Osc7501 'state=clear'
}

function Write-Status(
    [string] $Id,
    [string] $State,
    [string] $Title,
    [string] $Message,
    [string] $Extra = ''
) {
    $encodedTitle = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Title))
    $encodedMessage = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Message))
    $body = "state=$State`:title=$encodedTitle`:msg=$encodedMessage"
    if ($Id.Length -gt 0) { $body += ":id=$Id" }
    if ($Extra.Length -gt 0) { $body += ":$Extra" }
    Write-Osc7501 $body
}

try {
    Clear-Status
    [Console]::WriteLine('OSC 7501 mutation demo: four cycles, about 20 seconds. Ctrl+C stops and clears.')

    foreach ($cycle in 1..4) {
        [Console]::WriteLine("Cycle $cycle/4")
        Write-Status '' idle 'Tester console' "Waiting to start cycle $cycle" 'app=osc7501-demo'
        Start-Sleep -Milliseconds 500

        Write-Status '' working 'Tester console' "Running cycle $cycle" 'app=osc7501-demo:progress=0'
        Write-Status build working 'Build' 'Compiling sources' 'progress=10'
        Write-Status build/compile working 'Compiler' 'Resolving dependencies'
        Write-Status deploy idle 'Deployment' 'Waiting for the build'
        Start-Sleep -Milliseconds 500

        Write-Status '' working 'Tester console' "Building cycle $cycle" 'app=osc7501-demo:progress=20'
        Write-Status build working 'Build' 'Compilation in progress' 'progress=45'
        Write-Status build/compile working 'Compiler' 'Compiling application' 'progress=70'
        Start-Sleep -Milliseconds 500

        Write-Status build/compile done 'Compiler' 'Compilation complete'
        Write-Status build blocked 'Build' 'Approve running the tests' 'kind=permission:progress=60'
        Write-Status build/tests blocked 'Tests' 'Choose a test suite' 'kind=question'
        Start-Sleep -Milliseconds 500

        Write-Status build working 'Build' 'Running approved tests' 'progress=80'
        Write-Status build/tests working 'Tests' 'Executing test suite' 'progress=25'
        Write-Status build/tests/unit working 'Unit tests' 'Checking assertions' 'progress=50'
        Start-Sleep -Milliseconds 500

        Write-Status build/tests/unit error 'Unit tests' 'An assertion failed'
        Write-Status build/tests error 'Tests' 'Test suite failed'
        Write-Status build error 'Build' 'Retry required'
        Write-Status deploy blocked 'Deployment' 'Sign in before deploying' 'kind=auth'
        Start-Sleep -Milliseconds 500

        Write-Osc7501 'state=clear:id=build/tests'
        Write-Status build working 'Build retry' 'Retrying after fixing the assertion' 'progress=90'
        Write-Status build/tests working 'Tests retry' 'New test branch; previous descendants cleared' 'progress=75'
        Start-Sleep -Milliseconds 500

        Write-Status build/tests done 'Tests' 'All tests passed'
        Write-Status build done 'Build' 'Build ready'
        Write-Status deploy working 'Deployment' 'Uploading artifacts' 'progress=30'
        Write-Status deploy/east working 'East region' 'Rolling out' 'progress=65'
        Write-Status '' working 'Tester console' "Deploying cycle $cycle" 'app=osc7501-demo:progress=85'
        Start-Sleep -Milliseconds 500

        Write-Osc7501 'state=clear:id=build'
        Write-Status deploy/east done 'East region' 'Healthy'
        Write-Status deploy done 'Deployment' 'Deployment complete'
        Write-Status '' done 'Tester console' "Cycle $cycle finished" 'app=osc7501-demo'
        Start-Sleep -Milliseconds 500

        Clear-Status
        Start-Sleep -Milliseconds 500
    }

    [Console]::WriteLine('Finished; OSC 7501 inventory cleared.')
}
finally {
    Clear-Status
}
