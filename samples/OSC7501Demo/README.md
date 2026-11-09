# OSC7501Demo

A tester console for the [OSC 7501 program-status protocol](../../docs/program-status.md).

```sh
dotnet run --project samples/OSC7501Demo
```

Choose a shell from the startup picker. The demo detects executable `bash`, `zsh`,
`cmd.exe` (Windows only), and `pwsh.exe`/`pwsh` on `PATH`, showing each shell's
resolved path. Shell profiles are loaded normally, so your usual tools and
configuration remain available. No shell is launched until you choose one.

The embedded PTY terminal occupies roughly two thirds of the initial screen.
Drag the vertical divider to resize it. The right-hand tree follows the current
OSC 7501 inventory automatically, including slash-delimited IDs. Missing parent
records are shown as structural nodes marked `[no report]`, not invented reports.
Status icons distinguish idle (`○`), done (`✓`), blocked (`?`), and error (`✗`).
Working records show an animated dot spinner, including while no new reports are
arriving; structural parents use `+`. The expand/collapse indicator remains
separate from the status icon.
Click a node, or focus it and press Enter, to show all its decoded fields in the
bottom-right details panel. Long details wrap and can be scrolled with the mouse
wheel or by focusing the panel and using its scrolling keys.

Press **F6** to switch between the terminal and tree, and **F10** to quit.
Tab navigates the inspector controls; arrow keys navigate and expand/collapse
the tree. Ctrl+C remains available to programs inside the embedded terminal.
Shell exit leaves the inspector open so retained reports can still be examined.
Quitting disposes the child terminal and terminates its PTY process.

## Send test reports

For an automatic workout, run one of these scripts **inside the demo's embedded
shell**, from the repository root:

```sh
bash samples/OSC7501Demo/mutate-status.sh
```

```powershell
& ./samples/OSC7501Demo/mutate-status.ps1
```

Both run for about 20 seconds, mutating a root report and build/deployment
children and grandchildren. They exercise all five statuses, progress updates,
all three blocked kinds, changed titles/messages, retries, subtree clears, and
complete clears. The inventory ends empty; cleanup also clears it when you stop
with Ctrl+C. Each run clears any existing reports before starting.

In bash or zsh, emit a parent and child:

```sh
printf '\033]7501;state=working:id=build:app=tester:title=QnVpbGQ=\007'
printf '\033]7501;state=blocked:id=build/approval:kind=permission:progress=50:msg=QXBwcm92ZSBkZXBsb3k=\007'
```

In PowerShell:

```powershell
[Console]::Write("$([char]27)]7501;state=working:id=build:app=tester:title=QnVpbGQ=$([char]7)")
[Console]::Write("$([char]27)]7501;state=blocked:id=build/approval:kind=permission:progress=50:msg=QXBwcm92ZSBkZXBsb3k=$([char]7)")
```

These display a `Build` parent and an `Approve deploy` child. Each report is a
complete replacement, so repeat any fields you want to keep on the next update.
To finish or clear the branch in bash/zsh:

```sh
printf '\033]7501;state=done:id=build:app=tester:msg=QnVpbGQgcmVhZHk=\007'
printf '\033]7501;state=clear:id=build\007'
```

Any OSC-emitting program launched from any supported shell, including `cmd.exe`,
can report to the same inspector. This is a **current-state inspector**, not a
raw OSC log: malformed reports and other OSC families are not displayed.
Working/blocked reports expire on OSC 133 shell prompts and local process exit;
done/error reports remain. Ordinary shells may not emit OSC 133 unless shell
integration is configured. Updates and clears are reflected in the selected
details, even when that record disappears. Invisible formatting characters in
reported text are displayed as escapes rather than interpreted.
