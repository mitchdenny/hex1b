# Site recording driver

This build-time tool runs already-built standalone samples inside an outer
`Hex1bTerminal` and records HWT1 state. The samples use a published NuGet package;
the outer terminal and browser player use the current checkout.

`npm run samples --prefix src/site` prepares the catalog and invokes this tool.
The prepared JSON catalog supplies executable paths, dimensions, optional
readiness text, and optional key/text actions. Missing or empty actions record
only the starting state; no undeclared input is sent. No
sample-specific automation hooks or web hosting types are added to the app.

The driver requires visible initial content before capturing a full baseline.
Scenarios can provide `readyText` and action `waitFor` markers for stronger
readiness checks. Fixed pauses are viewing time, not substitutes for those
markers. Every captured frame is validated and acknowledged, and the browser
decoder validates the resulting recording again during the site build.

Normally capture finishes before cancellation terminates the child, so terminal
teardown is not part of playback. A finite flow can declare
`capturePolicy: "until-exit"`: the driver waits for natural completion, drains
output and captures a final full state before disposal. Reader and process tasks are observed and the PTY is
disposed on success and failure. A failed build, scenario, frame or recording
fails the site build rather than publishing an unavailable-demo placeholder.

Initial scenarios are deliberately short. Improve their metadata and readiness
markers as individual content areas are revised, without changing the sample's
consumer-facing code.
