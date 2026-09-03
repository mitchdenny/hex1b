using System.Text;

/// <summary>
/// Builds the numbered screen list the demo pages through.
/// </summary>
/// <remarks>
/// Numbering is positional and stable for a given build, so "screen 14" means the
/// same subject in a terminal, in the headless transcript, and in review.
/// </remarks>
internal static class DemoScreens
{
    /// <summary>
    /// Builds every screen: the grammar and geometry fixtures first, then the
    /// cursor, DECSDM, and margin scenes, then the graphics-state ownership
    /// scenes, then the transport scenes.
    /// </summary>
    public static IReadOnlyList<DemoScreen> Build(
        IReadOnlyList<RawSixelFixture> fixtures,
        IReadOnlyList<string> modelDescriptions,
        IReadOnlyList<RawCursorScene> cursorScenes,
        IReadOnlyList<RawGraphicsStateScene> graphicsStateScenes,
        IReadOnlyList<string> graphicsStateObservations,
        bool includeTransportScenes)
    {
        var screens = new List<DemoScreen>();
        var number = 1;

        for (var index = 0; index < fixtures.Count; index++)
        {
            var fixture = fixtures[index];
            var body = new List<byte>();
            if (fixture.SetupDcsBytes is { } setup)
            {
                body.AddRange(setup);
                body.AddRange(Encoding.ASCII.GetBytes("\r\n"));
            }

            body.AddRange(fixture.StandardDcsBytes);

            screens.Add(new DemoScreen(
                number++,
                fixture.Name,
                fixture.Expected,
                [.. body],
                Notes: [$"Model: {modelDescriptions[index]}"]));
        }

        foreach (var scene in cursorScenes)
        {
            // The scene owns its own cursor position, so it is emitted without the
            // renderer's body-row positioning already applied to it.
            screens.Add(new DemoScreen(
                number++,
                scene.Name,
                scene.Expected,
                scene.Bytes));
        }

        for (var index = 0; index < graphicsStateScenes.Count; index++)
        {
            var scene = graphicsStateScenes[index];
            screens.Add(new DemoScreen(
                number++,
                scene.Name,
                scene.Expected,
                scene.Bytes,
                Notes: [graphicsStateObservations[index]]));
        }

        if (!includeTransportScenes)
        {
            return screens;
        }

        screens.Add(new DemoScreen(
            number++,
            "Framing: two images, one write",
            "two red #FF0000 blocks stacked vertically, the first 240x60px (24 cells wide) and\n  the second 240x18px, both delivered in a single write with no transport boundary\n  between them. Both should appear complete and correctly separated",
            [
                .. fixtures[0].StandardDcsBytes,
                .. fixtures[1].StandardDcsBytes,
            ]));

        var split = new List<byte>();
        AddChunks(split, fixtures[0].StandardDcsBytes, [1, 1, 5, fixtures[0].StandardDcsBytes.Length - 9, 1, 1]);
        screens.Add(new DemoScreen(
            number++,
            "Framing: split writes",
            "the same solid red #FF0000 240x60px (24 cells wide) rectangle as\n  screen 1, but its DCS introducer, payload, and ESC-backslash terminator arrive in\n  six separate reads. It should look identical to screen 1: framing is transport-independent",
            [.. split]));

        screens.Add(new DemoScreen(
            number,
            "Framing: one byte at a time",
            "the same 240x6px (24 cells wide) all-green #00FF00 band as screen 4, delivered\n  one byte at a time. It should render identically: the parser reassembles the\n  image regardless of how the bytes are chunked",
            fixtures[3].StandardDcsBytes));

        return screens;
    }

    private static void AddChunks(List<byte> destination, byte[] bytes, IReadOnlyList<int> sizes)
    {
        var offset = 0;
        foreach (var size in sizes)
        {
            destination.AddRange(bytes.AsSpan(offset, size));
            offset += size;
        }

        if (offset != bytes.Length)
        {
            throw new InvalidOperationException("Demo split sizes must consume the complete DCS.");
        }
    }
}
