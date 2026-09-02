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
    /// cursor, DECSDM, and margin scenes, then the transport scenes.
    /// </summary>
    public static IReadOnlyList<DemoScreen> Build(
        IReadOnlyList<RawSixelFixture> fixtures,
        IReadOnlyList<string> modelDescriptions,
        IReadOnlyList<RawCursorScene> cursorScenes,
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

        if (!includeTransportScenes)
        {
            return screens;
        }

        screens.Add(new DemoScreen(
            number++,
            "Framing: two images, one write",
            "two consecutive DCS images arrive with no transport boundary between them",
            [
                .. fixtures[0].StandardDcsBytes,
                .. fixtures[1].StandardDcsBytes,
            ]));

        var split = new List<byte>();
        AddChunks(split, fixtures[0].StandardDcsBytes, [1, 1, 5, fixtures[0].StandardDcsBytes.Length - 9, 1, 1]);
        screens.Add(new DemoScreen(
            number++,
            "Framing: split writes",
            "the introducer, payload, and ESC-backslash terminator arrive in separate reads",
            [.. split]));

        screens.Add(new DemoScreen(
            number,
            "Framing: one byte at a time",
            "single-byte workload reads still form the original image upstream",
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
