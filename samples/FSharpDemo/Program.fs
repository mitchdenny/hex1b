// F# Demo - mirrors the repro from issue #321, adapted to use the
// Hex1bTerminal.CreateBuilder(...) pattern instead of constructing
// Hex1bApp directly.

open System
open Hex1b
open Hex1b.Events
open Hex1b.Widgets

// ToggleSwitchWidget is a controlled component: every reconcile resets the
// node's selection to whatever SelectedIndex the widget supplies. We hold
// the current selection here and feed it back in on each rebuild.
let mutable toggleIndex = 0

let buildWidget (_ctx: RootContext) : Hex1bWidget =
    let toggle =
        ToggleSwitchWidget(
            [| "正しいですか？"; "正しくないです。" |],
            toggleIndex
        )
            .OnSelectionChanged(fun (e: ToggleSelectionChangedEventArgs) ->
                toggleIndex <- e.SelectedIndex)

    let btns : Hex1bWidget seq =
        seq {
            ButtonWidget("第一个按钮") :> Hex1bWidget
            HStackWidget [|
                ButtonWidget("第二个按钮") :> Hex1bWidget
                TextBlockWidget " " :> Hex1bWidget
                ButtonWidget("第三个按钮") :> Hex1bWidget
            |] :> Hex1bWidget
            HStackWidget [|
                ButtonWidget("第二个按钮").Fill() :> Hex1bWidget
                TextBlockWidget " " :> Hex1bWidget
                ButtonWidget("第三个按钮") :> Hex1bWidget
            |] :> Hex1bWidget
            AlignWidget(ButtonWidget("第二个按钮"), Alignment.Center) :> Hex1bWidget
            AlignWidget(ButtonWidget("第二个按钮").FixedWidth(12), Alignment.Center) :> Hex1bWidget
            AlignWidget(toggle, Alignment.Center) :> Hex1bWidget
        }

    TabPanelWidget(
        [| TabItemWidget("한국어 제목", fun _ -> btns) |]
    ) :> Hex1bWidget

[<EntryPoint>]
let main _argv =
    use terminal =
        Hex1bTerminal
            .CreateBuilder()
            .WithMouse()
            .WithHex1bApp(fun _app _options ->
                Func<RootContext, Hex1bWidget>(buildWidget))
            .Build()

    terminal.RunAsync().GetAwaiter().GetResult() |> ignore
    0
