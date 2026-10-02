import type { TerminalCloseDetails } from "@hex1b/web-terminal";

export function workloadExitCode(close: TerminalCloseDetails): number | undefined {
  // 4000 and this reason format are the demo's contract, not a WebSocket or HMP1 exit-code API.
  if (close.code !== 4000) return undefined;
  const match = /^Workload exited with code (-?\d+)$/.exec(close.reason);
  if (!match) return undefined;
  const code = Number(match[1]);
  return Number.isInteger(code) && code >= -2147483648 && code <= 2147483647 ? code : undefined;
}

export function exitMessage(exitCode: number, preserved: boolean): { title: string; summary: string } {
  return {
    title: exitCode === 0 ? "Finished successfully" : "The program exited with an error",
    summary: (exitCode === 0 ? "All done! The program finished its work."
      : "The program could not finish successfully.") +
      (preserved ? " Your final output is kept behind this message. Choose View final output to inspect it."
        : " Final output was not retained. Enable Preserve final state before opening another view.")
  };
}
