using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using LogicBuilderDemo.Models;

/// <summary>
/// Builds the Logic Builder UI: a 5-section Grid with drag-and-drop action programming.
/// </summary>
internal static class LogicBuilderApp
{
    public static Hex1bWidget Build(RootContext ctx, AppState state)
    {
        return ctx.WindowPanel().Background(bg => BuildMainGrid(bg, state));
    }

    private static Hex1bWidget BuildMainGrid<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        return ctx.Grid(g =>
        {
            // 3 columns: inventory | content | command palette
            g.Columns.Add(SizeHint.Fixed(22));
            g.Columns.Add(SizeHint.Fill);
            g.Columns.Add(SizeHint.Fixed(26));

            // 3 rows: status bar | main area | action programmer
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(Math.Max(5, state.Tracks.Count * 2 + 3)));

            return [
                // Status bar (top, spans all columns)
                g.Cell(c => BuildStatusBar(c, state)).Row(0).ColumnSpan(0, 3),

                // Turtle Info (left)
                g.Cell(c => BuildTurtleInfo(c, state)).Row(1).Column(0),

                // Canvas (center)
                g.Cell(c => BuildCanvas(c, state)).Row(1).Column(1),

                // Command Palette (right)
                g.Cell(c => BuildCommandPalette(c, state)).Row(1).Column(2),

                // Action Programmer (bottom, spans all columns)
                g.Cell(c => BuildActionProgrammer(c, state)).Row(2).ColumnSpan(0, 3),
            ];
        });
    }

    private static Hex1bWidget BuildStatusBar<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        var totalSteps = state.Tracks.Sum(t => t.Steps.Count);
        var turtle = state.Turtle;
        return ctx.ThemePanel(
            t => t
                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(30, 30, 50))
                .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
            ctx.HStack(h => [
                h.Text(" LOGO Builder "),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                    h.Text($"| Pos:({turtle.X},{turtle.Y}) Heading:{turtle.Heading} Pen:{(turtle.PenDown ? "Down" : "Up")} Steps:{totalSteps} ")),
                h.Text("").Fill(),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                    h.Text("Ctrl+C to exit ")),
            ]));
    }

    private static Hex1bWidget BuildTurtleInfo<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        var turtle = state.Turtle;
        var items = new List<Hex1bWidget>();

        items.Add(ctx.ThemePanel(
            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
            ctx.Text(" Turtle")));
        items.Add(ctx.Separator());
        items.Add(ctx.Text($" Position: ({turtle.X}, {turtle.Y})"));
        items.Add(ctx.Text($" Heading:  {turtle.Heading} {turtle.Arrow}"));
        items.Add(ctx.Text($" Pen:      {(turtle.PenDown ? "Down #" : "Up")}"));
        items.Add(ctx.Text($" Lines:    {turtle.Canvas.Count}"));
        items.Add(ctx.Separator());

        if (state.IsRunning)
        {
            items.Add(ctx.Button(" Stop ")
                .OnClick(_ => state.StopExecution()));
        }
        else
        {
            items.Add(ctx.Button(" Run Program ")
                .OnClick(_ => state.StartExecution()));
        }

        items.Add(ctx.Button(" Reset ")
            .OnClick(_ => state.ResetAll()));
        items.Add(ctx.Text("").Fill());

        return ctx.Border(
            ctx.VStack(_ => [.. items])
        ).Title("Turtle");
    }

    private static Hex1bWidget BuildCanvas<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        var turtle = state.Turtle;

        // Determine canvas viewport (centered on origin, show at least 40x20)
        const int halfW = 20;
        const int halfH = 10;

        var lines = new List<Hex1bWidget>();
        for (int row = -halfH; row <= halfH; row++)
        {
            var chars = new char[halfW * 2 + 1];
            for (int col = -halfW; col <= halfW; col++)
            {
                int idx = col + halfW;
                if (col == turtle.X && row == turtle.Y)
                    chars[idx] = turtle.Arrow;
                else if (turtle.Canvas.TryGetValue((col, row), out var c))
                    chars[idx] = c;
                else if (col == 0 && row == 0)
                    chars[idx] = '.';
                else
                    chars[idx] = ' ';
            }

            var lineStr = new string(chars);

            // Highlight the turtle position if it's in this row and within viewport
            var turtleCol = turtle.X + halfW;
            if (row == turtle.Y && turtleCol >= 0 && turtleCol < chars.Length)
            {
                lines.Add(ctx.HStack(h =>
                {
                    var before = lineStr[..turtleCol];
                    var turtleCh = lineStr[turtleCol..(turtleCol + 1)];
                    var after = lineStr[(turtleCol + 1)..];
                    return [
                        h.Text(before),
                        h.ThemePanel(
                            t => t
                                .Set(GlobalTheme.ForegroundColor, Hex1bColor.Green)
                                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(30, 60, 30)),
                            h.Text(turtleCh)),
                        h.Text(after),
                    ];
                }));
            }
            else
            {
                lines.Add(ctx.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 180, 120)),
                    ctx.Text(lineStr)));
            }
        }

        return ctx.Border(
            ctx.VStack(_ => [.. lines])
        ).Title("Canvas");
    }

    private static Hex1bWidget BuildCommandPalette<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        return ctx.Droppable(dc =>
        {
            var borderColor = dc.IsHoveredByDrag
                ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
                : Hex1bColor.Magenta;

            var items = new List<Hex1bWidget>();

            items.Add(dc.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Magenta),
                dc.Text(dc.IsHoveredByDrag && dc.CanAcceptDrag
                    ? " Drop to save!"
                    : " Commands")));
            items.Add(dc.Separator());

            // Built-in commands (draggable)
            foreach (var cmd in state.PaletteCommands)
            {
                items.Add(dc.Draggable(cmd, drag =>
                {
                    if (drag.IsDragging)
                    {
                        return drag.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                            drag.Text("  ----------"));
                    }

                    return drag.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
                        drag.Text($"  {cmd.Display}"));
                })
                .DragOverlay(drag =>
                    drag.ThemePanel(
                        t => t
                            .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 20, 60))
                            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                        drag.Text($" {cmd.Display} "))));
            }

            // Saved sequences section (draggable back as subroutines)
            if (state.SavedSequences.Count > 0)
            {
                items.Add(dc.Text(""));
                items.Add(dc.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
                    dc.Text(" Saved")));
                items.Add(dc.Separator());

                foreach (var seq in state.SavedSequences)
                {
                    var preview = string.Join(" ", seq.Steps.Select(s => s.Display));
                    items.Add(dc.Draggable(seq, drag =>
                    {
                        if (drag.IsDragging)
                        {
                            return drag.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                                drag.Text("  ----------"));
                        }

                        return drag.VStack(v => [
                            v.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                                v.Text($"  {seq.Display}")),
                            v.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                                v.Text($"    {preview}")),
                        ]);
                    })
                    .DragOverlay(drag =>
                        drag.ThemePanel(
                            t => t
                                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 20, 60))
                                .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                            drag.Text($" {seq.Display} "))));
                }
            }

            items.Add(dc.Text("").Fill());

            return dc.ThemePanel(
                t => t.Set(BorderTheme.BorderColor, borderColor),
                dc.Border(dc.VStack(_ => [.. items])));
        })
        .Accept(data => data is Track)
        .OnDrop(e =>
        {
            if (e.DragData is Track track && track.Steps.Count > 0)
            {
                var stepsToSave = track.Steps.ToList();
                var saveName = track.Name;

                var window = e.Windows.Window(w => w.VStack(v => [
                    v.Text(""),
                    v.Text("  Save this sequence to the"),
                    v.Text("  command palette:"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("  Name: "),
                        h.TextBox(saveName)
                            .OnTextChanged(ev => saveName = ev.NewText)
                            .OnSubmit(ev =>
                            {
                                if (!string.IsNullOrWhiteSpace(saveName))
                                {
                                    state.SaveTrackAsSequence(saveName.Trim(), stepsToSave);
                                    ev.Windows.Close(w.Window);
                                }
                            })
                            .Fill(),
                    ]),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("  "),
                        h.Button(" Save ").OnClick(ev =>
                        {
                            if (!string.IsNullOrWhiteSpace(saveName))
                            {
                                state.SaveTrackAsSequence(saveName.Trim(), stepsToSave);
                                ev.Windows.Close(w.Window);
                            }
                        }),
                        h.Text(" "),
                        h.Button(" Cancel ").OnClick(ev => ev.Windows.Close(w.Window)),
                    ]),
                ]))
                .Title("Save Sequence")
                .Size(40, 11)
                .Position(WindowPositionSpec.Center);

                e.Windows.Open(window);
            }
        })
;
    }

    private static Hex1bWidget BuildActionProgrammer<T>(WidgetContext<T> ctx, AppState state) where T : Hex1bWidget
    {
        return ctx.Border(
            ctx.VStack(v =>
            {
                var items = new List<Hex1bWidget>();

                items.Add(v.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                    v.Text(" Action Programmer")));
                items.Add(v.Separator());

                // Render each track
                for (int ti = 0; ti < state.Tracks.Count; ti++)
                {
                    items.Add(BuildTrackRow(v, state.Tracks[ti], ti, state));
                }

                // Add Track button
                items.Add(v.HStack(h => [
                    h.Button(" + Add Track ").OnClick(_ =>
                    {
                        state.AddTrack();
                    }),
                    h.Text("").Fill(),
                ]));

                return [.. items];
            })
        ).Title("Action Programmer");
    }

    /// <summary>
    /// Wraps a step being dragged from a specific position on a track, enabling reordering.
    /// </summary>
    private record TrackStepDrag(ITrackStep Step, Track SourceTrack, int SourceIndex);

    private static Hex1bWidget BuildTrackRow<T>(WidgetContext<T> ctx, Track track, int trackIndex, AppState state) where T : Hex1bWidget
    {
        // The whole row is a droppable target for commands
        return ctx.Droppable(dc =>
        {
            var borderColor = dc.IsHoveredByDrag && dc.CanAcceptDrag
                ? Hex1bColor.Green
                : Hex1bColor.FromRgb(60, 60, 80);

            return dc.ThemePanel(
                t => t.Set(BorderTheme.BorderColor, borderColor),
                dc.HStack(h =>
                {
                    var parts = new List<Hex1bWidget>();

                    // Only the track label is draggable (back to command palette)
                    parts.Add(h.Draggable(track, dragCtx =>
                    {
                        if (dragCtx.IsDragging)
                        {
                            return dragCtx.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                                dragCtx.Text($" ┄┄┄┄┄ "));
                        }

                        return dragCtx.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
                            dragCtx.Text($" {track.Name}: "));
                    })
                    .DragOverlay(drag =>
                        drag.ThemePanel(
                            t => t
                                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(30, 30, 50))
                                .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
                            drag.Text($" {track.Name} ({track.Steps.Count} steps, cost:{track.TotalCost}) "))));

                    // Drop target before first command (always present)
                    parts.Add(dc.DropTarget("pos-0", dt =>
                        BuildTrackDropIndicator(dt, track.Steps.Count == 0)));

                    for (int i = 0; i < track.Steps.Count; i++)
                    {
                        var step = track.Steps[i];
                        var stepIndex = i;
                        var dragData = new TrackStepDrag(step, track, i);

                        var isSub = step is SavedSequence;
                        var glyphBg = isSub
                            ? Hex1bColor.FromRgb(60, 120, 60)
                            : Hex1bColor.FromRgb(80, 80, 140);

                        var capturedStep = step;
                        var capturedTrack = track;
                        var capturedIndex = stepIndex;

                        // Determine if this step is currently being executed
                        var execPos = state.CurrentExecutionPosition;
                        var isExecuting = execPos.HasValue
                            && execPos.Value.TrackIndex == trackIndex
                            && execPos.Value.StepIndex == capturedIndex;

                        var chipBg = isExecuting
                            ? Hex1bColor.FromRgb(80, 60, 20)
                            : Hex1bColor.FromRgb(35, 35, 50);

                        // Check if this step is being dragged (set by Draggable builder on previous render)
                        var isDragging = state.DraggingStep is var ds
                            && ds.HasValue
                            && ds.Value.Track == capturedTrack
                            && ds.Value.Index == capturedIndex;

                        parts.Add(h.HStack(row =>
                        {
                            var draggable = row.Draggable(dragData, dragCtx =>
                            {
                                // Update shared state so next render knows which step is dragged
                                if (dragCtx.IsDragging)
                                    state.DraggingStep = (capturedTrack, capturedIndex);
                                else if (state.DraggingStep is var cur
                                         && cur.HasValue
                                         && cur.Value.Track == capturedTrack
                                         && cur.Value.Index == capturedIndex)
                                    state.DraggingStep = null;

                                // When dragging, the chip disappears from the track
                                if (dragCtx.IsDragging)
                                    return dragCtx.Text("");

                                return dragCtx.HStack(chip => [
                                    // Glyph on colored background
                                    chip.ThemePanel(
                                        t => t
                                            .Set(GlobalTheme.BackgroundColor, glyphBg)
                                            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
                                        chip.Text(capturedStep.Glyph)),
                                    // Name with padding
                                    chip.ThemePanel(
                                        t => t
                                            .Set(GlobalTheme.BackgroundColor, chipBg)
                                            .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 200, 220)),
                                        chip.Text($" {capturedStep.Name} ")),
                                    // Cost
                                    chip.ThemePanel(
                                        t => t
                                            .Set(GlobalTheme.BackgroundColor, chipBg)
                                            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
                                        chip.Text($"{capturedStep.Cost}"))
                                ]);
                            })
                            .DragOverlay(drag =>
                                drag.ThemePanel(
                                    t => t
                                        .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 20, 60))
                                        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                                    drag.Text($" {capturedStep.Display} ")));

                            // Hide delete icon while this step is being dragged
                            if (isDragging)
                                return [draggable];

                            var deleteIcon = row.Icon("x")
                                .OnClick(_ =>
                                {
                                    if (capturedIndex < capturedTrack.Steps.Count)
                                        capturedTrack.Steps.RemoveAt(capturedIndex);
                                });

                            return [draggable, deleteIcon];
                        }));

                        // Drop target after each step
                        parts.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                            BuildTrackDropIndicator(dt, false)));
                    }

                    parts.Add(h.Text("").Fill());
                    return [.. parts];
                }));
        })
        .Accept(data => data is Command or TrackStepDrag or SavedSequence)
        .OnDropTarget(e =>
        {
            var posIndex = int.Parse(e.TargetId.Split('-')[1]);

            if (e.DragData is TrackStepDrag td)
            {
                td.SourceTrack.Steps.RemoveAt(td.SourceIndex);
                if (td.SourceTrack == track && posIndex > td.SourceIndex)
                    posIndex--;
                posIndex = Math.Min(posIndex, track.Steps.Count);
                track.Steps.Insert(posIndex, td.Step);
            }
            else if (e.DragData is SavedSequence seq)
            {
                // Insert as subroutine (not expanded)
                posIndex = Math.Min(posIndex, track.Steps.Count);
                track.Steps.Insert(posIndex, seq);
            }
            else if (e.DragData is Command cmd)
            {
                posIndex = Math.Min(posIndex, track.Steps.Count);
                track.Steps.Insert(posIndex, cmd);
            }
        })
        .OnDrop(e =>
        {
            // Fallback: append to end
            if (e.DragData is TrackStepDrag td)
            {
                td.SourceTrack.Steps.RemoveAt(td.SourceIndex);
                track.Steps.Add(td.Step);
            }
            else if (e.DragData is SavedSequence seq)
            {
                track.Steps.Add(seq);
            }
            else if (e.DragData is Command cmd)
            {
                track.Steps.Add(cmd);
            }
        });
    }

    private static Hex1bWidget BuildTrackDropIndicator(DropTargetContext dt, bool isEmpty)
    {
        if (dt.IsActive)
        {
            return dt.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Red),
                dt.Text(isEmpty ? "| drop here" : "|"));
        }

        if (isEmpty)
        {
            return dt.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                dt.Text("(drag commands here)"));
        }

        return dt.Text(" ");
    }
}
