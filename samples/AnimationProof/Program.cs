using Hex1b;
using Hex1b.Widgets;

// Phase 0: Prove that widget-layer composition with spinner animation works ✅
// Phase 1: Test new TreeContext API ✅
// Phase 2: Test async loading with externalized state and Loading() flag
// 
// This sample tests:
// 1. Conditional icon/spinner composition (PASSED)
// 2. New Tree API with TreeContext callback pattern (PASSED)
// 3. Externalized loading state with Loading() flag

var isLoading = false;

// Externalized loading state for tree
var serverLoading = false;
var serverChildren = new List<TreeItemWidget>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("🧪 Animation & Tree API Proof-of-Concept"),
        v.Text(""),
        
        // Test 1: Conditional icon/spinner composition
        v.Border(b => [
            b.Text("Test 1: Conditional Icon/Spinner Composition"),
            b.Text(""),
            b.HStack(h => [
                h.Text("Status: "),
                isLoading 
                    ? h.Spinner()  // When loading, show animated spinner
                    : h.Icon("📁"),  // When not loading, show static icon
                h.Text(isLoading ? " Loading..." : " Ready")
            ]),
            b.Text(""),
            b.Button(isLoading ? "Stop Loading" : "Start Loading")
                .OnClick(_ => { 
                    isLoading = !isLoading;
                    app.Invalidate();
                })
        ], title: "✅ Animation Test"),
        
        v.Text(""),
        
        // Test 2: New Tree API with TreeContext (static)
        v.Border(b => [
            b.Text("Test 2: Static Tree with TreeContext API"),
            b.Text(""),
            b.Tree(t => [
                t.Item("Root", root => [
                    root.Item("Documents", docs => [
                        docs.Item("report.docx"),
                        docs.Item("notes.txt")
                    ]).Icon("📁"),
                    root.Item("Pictures", pics => [
                        pics.Item("photo.jpg")
                    ]).Icon("📸")
                ]).Expanded().Icon("🏠"),
                t.Item("Standalone Item").Icon("📄")
            ])
        ], title: "✅ Static Tree"),
        
        v.Text(""),
        
        // Test 3: Externalized loading state with Loading() flag
        v.Border(b => [
            b.Text("Test 3: Externalized Loading State"),
            b.Text("Click ▶ to expand - spinner should animate for 3 seconds"),
            b.Text(""),
            b.Tree(t => [
                t.Item("Remote Server", _ => serverChildren)
                    .Loading(serverLoading)  // Show spinner when loading
                    .Expanded(serverLoading || serverChildren.Count > 0)
                    .Icon("🖥️")
                    .OnExpanding(async args => {
                        // Set loading state and invalidate to show spinner
                        serverLoading = true;
                        app.Invalidate();
                        
                        // Simulate network delay
                        await Task.Delay(3000);
                        
                        // Return loaded children
                        var childContext = new TreeContext();
                        serverChildren = [
                            childContext.Item("Users").Icon("👥"),
                            childContext.Item("Logs").Icon("📋"),
                            childContext.Item("Config").Icon("⚙️")
                        ];
                        
                        // Clear loading state
                        serverLoading = false;
                        app.Invalidate();
                        
                        return serverChildren;
                    })
            ])
        ], title: "🔄 Externalized Loading Test"),
        
        v.Text(""),
        v.Separator(),
        v.Text("Press Ctrl+C to exit")
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
