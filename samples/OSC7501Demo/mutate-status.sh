#!/usr/bin/env bash
set -euo pipefail

emit() {
    printf '\033]7501;%s\007' "$1"
}

clear_all() {
    emit 'state=clear'
}

pause() {
    sleep 0.5
    if (( SECONDS >= 20 )); then
        clear_all
        printf 'Finished; OSC 7501 inventory cleared.\n'
        exit 0
    fi
}

report() {
    local id=$1 state=$2 title=$3 message=$4 extra=${5:-}
    local encoded_title encoded_message body
    encoded_title=$(printf '%s' "$title" | base64 | tr -d '\r\n')
    encoded_message=$(printf '%s' "$message" | base64 | tr -d '\r\n')
    body="state=$state:title=$encoded_title:msg=$encoded_message"
    if [[ -n "$id" ]]; then body="$body:id=$id"; fi
    if [[ -n "$extra" ]]; then body="$body:$extra"; fi
    emit "$body"
}

trap clear_all EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

clear_all
printf 'OSC 7501 mutation demo: about 20 seconds. Ctrl+C stops and clears.\n'
SECONDS=0

for ((cycle = 1; ; cycle++)); do
    printf 'Cycle %s\n' "$cycle"
    report '' idle 'Tester console' "Waiting to start cycle $cycle" 'app=osc7501-demo'
    pause

    report '' working 'Tester console' "Running cycle $cycle" 'app=osc7501-demo:progress=0'
    report build working 'Build' 'Compiling sources' 'progress=10'
    report build/compile working 'Compiler' 'Resolving dependencies'
    report deploy idle 'Deployment' 'Waiting for the build'
    pause

    report '' working 'Tester console' "Building cycle $cycle" 'app=osc7501-demo:progress=20'
    report build working 'Build' 'Compilation in progress' 'progress=45'
    report build/compile working 'Compiler' 'Compiling application' 'progress=70'
    pause

    report build/compile done 'Compiler' 'Compilation complete'
    report build blocked 'Build' 'Approve running the tests' 'kind=permission:progress=60'
    report build/tests blocked 'Tests' 'Choose a test suite' 'kind=question'
    pause

    report build working 'Build' 'Running approved tests' 'progress=80'
    report build/tests working 'Tests' 'Executing test suite' 'progress=25'
    report build/tests/unit working 'Unit tests' 'Checking assertions' 'progress=50'
    pause

    report build/tests/unit error 'Unit tests' 'An assertion failed'
    report build/tests error 'Tests' 'Test suite failed'
    report build error 'Build' 'Retry required'
    report deploy blocked 'Deployment' 'Sign in before deploying' 'kind=auth'
    pause

    emit 'state=clear:id=build/tests'
    report build working 'Build retry' 'Retrying after fixing the assertion' 'progress=90'
    report build/tests working 'Tests retry' 'New test branch; previous descendants cleared' 'progress=75'
    pause

    report build/tests done 'Tests' 'All tests passed'
    report build done 'Build' 'Build ready'
    report deploy working 'Deployment' 'Uploading artifacts' 'progress=30'
    report deploy/east working 'East region' 'Rolling out' 'progress=65'
    report '' working 'Tester console' "Deploying cycle $cycle" 'app=osc7501-demo:progress=85'
    pause

    emit 'state=clear:id=build'
    report deploy/east done 'East region' 'Healthy'
    report deploy done 'Deployment' 'Deployment complete'
    report '' done 'Tester console' "Cycle $cycle finished" 'app=osc7501-demo'
    pause

    clear_all
    pause
done
