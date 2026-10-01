using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalKeyboardMatrixTests
{
    // Literal wire bytes, not expectations calculated by the production key/text mapper.
    // Columns: plain, Shift, Alt, Alt+Shift, Ctrl, Ctrl+Shift, Ctrl+Alt, Ctrl+Alt+Shift.
    // This is Hex1b's legacy automation profile: US-style symbols, C0 letter mapping,
    // no extra Ctrl+digit/punctuation aliases, and numeric keypad identity (not NumLock).
    private static readonly KeyVector[] Vectors =
    [
        new(Hex1bKey.A, "61 41 1B61 1B41 01 01 1B01 1B01"),
        new(Hex1bKey.B, "62 42 1B62 1B42 02 02 1B02 1B02"),
        new(Hex1bKey.C, "63 43 1B63 1B43 03 03 1B03 1B03"),
        new(Hex1bKey.D, "64 44 1B64 1B44 04 04 1B04 1B04"),
        new(Hex1bKey.E, "65 45 1B65 1B45 05 05 1B05 1B05"),
        new(Hex1bKey.F, "66 46 1B66 1B46 06 06 1B06 1B06"),
        new(Hex1bKey.G, "67 47 1B67 1B47 07 07 1B07 1B07"),
        new(Hex1bKey.H, "68 48 1B68 1B48 08 08 1B08 1B08"),
        new(Hex1bKey.I, "69 49 1B69 1B49 09 09 1B09 1B09"),
        new(Hex1bKey.J, "6A 4A 1B6A 1B4A 0A 0A 1B0A 1B0A"),
        new(Hex1bKey.K, "6B 4B 1B6B 1B4B 0B 0B 1B0B 1B0B"),
        new(Hex1bKey.L, "6C 4C 1B6C 1B4C 0C 0C 1B0C 1B0C"),
        new(Hex1bKey.M, "6D 4D 1B6D 1B4D 0D 0D 1B0D 1B0D"),
        new(Hex1bKey.N, "6E 4E 1B6E 1B4E 0E 0E 1B0E 1B0E"),
        new(Hex1bKey.O, "6F 4F 1B6F 1B4F 0F 0F 1B0F 1B0F"),
        new(Hex1bKey.P, "70 50 1B70 1B50 10 10 1B10 1B10"),
        new(Hex1bKey.Q, "71 51 1B71 1B51 11 11 1B11 1B11"),
        new(Hex1bKey.R, "72 52 1B72 1B52 12 12 1B12 1B12"),
        new(Hex1bKey.S, "73 53 1B73 1B53 13 13 1B13 1B13"),
        new(Hex1bKey.T, "74 54 1B74 1B54 14 14 1B14 1B14"),
        new(Hex1bKey.U, "75 55 1B75 1B55 15 15 1B15 1B15"),
        new(Hex1bKey.V, "76 56 1B76 1B56 16 16 1B16 1B16"),
        new(Hex1bKey.W, "77 57 1B77 1B57 17 17 1B17 1B17"),
        new(Hex1bKey.X, "78 58 1B78 1B58 18 18 1B18 1B18"),
        new(Hex1bKey.Y, "79 59 1B79 1B59 19 19 1B19 1B19"),
        new(Hex1bKey.Z, "7A 5A 1B7A 1B5A 1A 1A 1B1A 1B1A"),
        new(Hex1bKey.D0, "30 29 1B30 1B29 30 29 1B30 1B29"),
        new(Hex1bKey.D1, "31 21 1B31 1B21 31 21 1B31 1B21"),
        new(Hex1bKey.D2, "32 40 1B32 1B40 32 00 1B32 1B00"),
        new(Hex1bKey.D3, "33 23 1B33 1B23 33 23 1B33 1B23"),
        new(Hex1bKey.D4, "34 24 1B34 1B24 34 24 1B34 1B24"),
        new(Hex1bKey.D5, "35 25 1B35 1B25 35 25 1B35 1B25"),
        new(Hex1bKey.D6, "36 5E 1B36 1B5E 36 1E 1B36 1B1E"),
        new(Hex1bKey.D7, "37 26 1B37 1B26 37 26 1B37 1B26"),
        new(Hex1bKey.D8, "38 2A 1B38 1B2A 38 2A 1B38 1B2A"),
        new(Hex1bKey.D9, "39 28 1B39 1B28 39 28 1B39 1B28"),
        new(Hex1bKey.F1, "1B4F50 1B5B313B3250 1B5B313B3350 1B5B313B3450 1B5B313B3550 1B5B313B3650 1B5B313B3750 1B5B313B3850"),
        new(Hex1bKey.F2, "1B4F51 1B5B313B3251 1B5B313B3351 1B5B313B3451 1B5B313B3551 1B5B313B3651 1B5B313B3751 1B5B313B3851"),
        new(Hex1bKey.F3, "1B4F52 1B5B313B3252 1B5B313B3352 1B5B313B3452 1B5B313B3552 1B5B313B3652 1B5B313B3752 1B5B313B3852"),
        new(Hex1bKey.F4, "1B4F53 1B5B313B3253 1B5B313B3353 1B5B313B3453 1B5B313B3553 1B5B313B3653 1B5B313B3753 1B5B313B3853"),
        new(Hex1bKey.F5, "1B5B31357E 1B5B31353B327E 1B5B31353B337E 1B5B31353B347E 1B5B31353B357E 1B5B31353B367E 1B5B31353B377E 1B5B31353B387E"),
        new(Hex1bKey.F6, "1B5B31377E 1B5B31373B327E 1B5B31373B337E 1B5B31373B347E 1B5B31373B357E 1B5B31373B367E 1B5B31373B377E 1B5B31373B387E"),
        new(Hex1bKey.F7, "1B5B31387E 1B5B31383B327E 1B5B31383B337E 1B5B31383B347E 1B5B31383B357E 1B5B31383B367E 1B5B31383B377E 1B5B31383B387E"),
        new(Hex1bKey.F8, "1B5B31397E 1B5B31393B327E 1B5B31393B337E 1B5B31393B347E 1B5B31393B357E 1B5B31393B367E 1B5B31393B377E 1B5B31393B387E"),
        new(Hex1bKey.F9, "1B5B32307E 1B5B32303B327E 1B5B32303B337E 1B5B32303B347E 1B5B32303B357E 1B5B32303B367E 1B5B32303B377E 1B5B32303B387E"),
        new(Hex1bKey.F10, "1B5B32317E 1B5B32313B327E 1B5B32313B337E 1B5B32313B347E 1B5B32313B357E 1B5B32313B367E 1B5B32313B377E 1B5B32313B387E"),
        new(Hex1bKey.F11, "1B5B32337E 1B5B32333B327E 1B5B32333B337E 1B5B32333B347E 1B5B32333B357E 1B5B32333B367E 1B5B32333B377E 1B5B32333B387E"),
        new(Hex1bKey.F12, "1B5B32347E 1B5B32343B327E 1B5B32343B337E 1B5B32343B347E 1B5B32343B357E 1B5B32343B367E 1B5B32343B377E 1B5B32343B387E"),
        new(Hex1bKey.UpArrow,
            "1B5B41 1B5B313B3241 1B5B313B3341 1B5B313B3441 1B5B313B3541 1B5B313B3641 1B5B313B3741 1B5B313B3841",
            Cursor: "1B4F41 1B5B313B3241 1B5B313B3341 1B5B313B3441 1B5B313B3541 1B5B313B3641 1B5B313B3741 1B5B313B3841"),
        new(Hex1bKey.DownArrow,
            "1B5B42 1B5B313B3242 1B5B313B3342 1B5B313B3442 1B5B313B3542 1B5B313B3642 1B5B313B3742 1B5B313B3842",
            Cursor: "1B4F42 1B5B313B3242 1B5B313B3342 1B5B313B3442 1B5B313B3542 1B5B313B3642 1B5B313B3742 1B5B313B3842"),
        new(Hex1bKey.LeftArrow,
            "1B5B44 1B5B313B3244 1B5B313B3344 1B5B313B3444 1B5B313B3544 1B5B313B3644 1B5B313B3744 1B5B313B3844",
            Cursor: "1B4F44 1B5B313B3244 1B5B313B3344 1B5B313B3444 1B5B313B3544 1B5B313B3644 1B5B313B3744 1B5B313B3844"),
        new(Hex1bKey.RightArrow,
            "1B5B43 1B5B313B3243 1B5B313B3343 1B5B313B3443 1B5B313B3543 1B5B313B3643 1B5B313B3743 1B5B313B3843",
            Cursor: "1B4F43 1B5B313B3243 1B5B313B3343 1B5B313B3443 1B5B313B3543 1B5B313B3643 1B5B313B3743 1B5B313B3843"),
        new(Hex1bKey.Home,
            "1B5B48 1B5B313B3248 1B5B313B3348 1B5B313B3448 1B5B313B3548 1B5B313B3648 1B5B313B3748 1B5B313B3848",
            Cursor: "1B4F48 1B5B313B3248 1B5B313B3348 1B5B313B3448 1B5B313B3548 1B5B313B3648 1B5B313B3748 1B5B313B3848"),
        new(Hex1bKey.End,
            "1B5B46 1B5B313B3246 1B5B313B3346 1B5B313B3446 1B5B313B3546 1B5B313B3646 1B5B313B3746 1B5B313B3846",
            Cursor: "1B4F46 1B5B313B3246 1B5B313B3346 1B5B313B3446 1B5B313B3546 1B5B313B3646 1B5B313B3746 1B5B313B3846"),
        new(Hex1bKey.PageUp, "1B5B357E 1B5B353B327E 1B5B353B337E 1B5B353B347E 1B5B353B357E 1B5B353B367E 1B5B353B377E 1B5B353B387E"),
        new(Hex1bKey.PageDown, "1B5B367E 1B5B363B327E 1B5B363B337E 1B5B363B347E 1B5B363B357E 1B5B363B367E 1B5B363B377E 1B5B363B387E"),
        new(Hex1bKey.Backspace, "7F 7F 1B7F 1B7F 08 08 1B08 1B08"),
        new(Hex1bKey.Delete, "1B5B337E 1B5B333B327E 1B5B333B337E 1B5B333B347E 1B5B333B357E 1B5B333B367E 1B5B333B377E 1B5B333B387E"),
        new(Hex1bKey.Insert, "1B5B327E 1B5B323B327E 1B5B323B337E 1B5B323B347E 1B5B323B357E 1B5B323B367E 1B5B323B377E 1B5B323B387E"),
        new(Hex1bKey.Tab, "09 1B5B5A 1B09 1B1B5B5A 09 1B5B5A 1B09 1B1B5B5A"),
        new(Hex1bKey.Enter, "0D 0D 1B0D 1B0D 0D 0D 1B0D 1B0D"),
        new(Hex1bKey.Spacebar, "20 20 1B20 1B20 00 00 1B00 1B00"),
        new(Hex1bKey.Escape, "1B 1B 1B1B 1B1B 1B 1B 1B1B 1B1B"),
        new(Hex1bKey.OemComma, "2C 3C 1B2C 1B3C 2C 3C 1B2C 1B3C"),
        new(Hex1bKey.OemPeriod, "2E 3E 1B2E 1B3E 2E 3E 1B2E 1B3E"),
        new(Hex1bKey.OemMinus, "2D 5F 1B2D 1B5F 2D 1F 1B2D 1B1F"),
        new(Hex1bKey.OemPlus, "3D 2B 1B3D 1B2B 3D 2B 1B3D 1B2B"),
        new(Hex1bKey.OemQuestion, "2F 3F 1B2F 1B3F 2F 3F 1B2F 1B3F"),
        new(Hex1bKey.Oem1, "3B 3A 1B3B 1B3A 3B 3A 1B3B 1B3A"),
        new(Hex1bKey.Oem4, "5B 7B 1B5B 1B7B 1B 7B 1B1B 1B7B"),
        new(Hex1bKey.Oem5, "5C 7C 1B5C 1B7C 1C 7C 1B1C 1B7C"),
        new(Hex1bKey.Oem6, "5D 7D 1B5D 1B7D 1D 7D 1B1D 1B7D"),
        new(Hex1bKey.Oem7, "27 22 1B27 1B22 27 22 1B27 1B22"),
        new(Hex1bKey.OemTilde, "60 7E 1B60 1B7E 60 7E 1B60 1B7E"),
        // Xterm-style application keypad: SS3, optional modifier 2-8, keypad final.
        // Independent reference: ghostty-org/ghostty 5de703a1, src/input/function_keys.zig kpKeys.
        new(Hex1bKey.NumPad0, "30 30 1B30 1B30 30 30 1B30 1B30",
            Keypad: "1B4F70 1B4F3270 1B4F3370 1B4F3470 1B4F3570 1B4F3670 1B4F3770 1B4F3870"),
        new(Hex1bKey.NumPad1, "31 31 1B31 1B31 31 31 1B31 1B31",
            Keypad: "1B4F71 1B4F3271 1B4F3371 1B4F3471 1B4F3571 1B4F3671 1B4F3771 1B4F3871"),
        new(Hex1bKey.NumPad2, "32 32 1B32 1B32 32 32 1B32 1B32",
            Keypad: "1B4F72 1B4F3272 1B4F3372 1B4F3472 1B4F3572 1B4F3672 1B4F3772 1B4F3872"),
        new(Hex1bKey.NumPad3, "33 33 1B33 1B33 33 33 1B33 1B33",
            Keypad: "1B4F73 1B4F3273 1B4F3373 1B4F3473 1B4F3573 1B4F3673 1B4F3773 1B4F3873"),
        new(Hex1bKey.NumPad4, "34 34 1B34 1B34 34 34 1B34 1B34",
            Keypad: "1B4F74 1B4F3274 1B4F3374 1B4F3474 1B4F3574 1B4F3674 1B4F3774 1B4F3874"),
        new(Hex1bKey.NumPad5, "35 35 1B35 1B35 35 35 1B35 1B35",
            Keypad: "1B4F75 1B4F3275 1B4F3375 1B4F3475 1B4F3575 1B4F3675 1B4F3775 1B4F3875"),
        new(Hex1bKey.NumPad6, "36 36 1B36 1B36 36 36 1B36 1B36",
            Keypad: "1B4F76 1B4F3276 1B4F3376 1B4F3476 1B4F3576 1B4F3676 1B4F3776 1B4F3876"),
        new(Hex1bKey.NumPad7, "37 37 1B37 1B37 37 37 1B37 1B37",
            Keypad: "1B4F77 1B4F3277 1B4F3377 1B4F3477 1B4F3577 1B4F3677 1B4F3777 1B4F3877"),
        new(Hex1bKey.NumPad8, "38 38 1B38 1B38 38 38 1B38 1B38",
            Keypad: "1B4F78 1B4F3278 1B4F3378 1B4F3478 1B4F3578 1B4F3678 1B4F3778 1B4F3878"),
        new(Hex1bKey.NumPad9, "39 39 1B39 1B39 39 39 1B39 1B39",
            Keypad: "1B4F79 1B4F3279 1B4F3379 1B4F3479 1B4F3579 1B4F3679 1B4F3779 1B4F3879"),
        new(Hex1bKey.Multiply, "2A 2A 1B2A 1B2A 2A 2A 1B2A 1B2A",
            Keypad: "1B4F6A 1B4F326A 1B4F336A 1B4F346A 1B4F356A 1B4F366A 1B4F376A 1B4F386A"),
        new(Hex1bKey.Add, "2B 2B 1B2B 1B2B 2B 2B 1B2B 1B2B",
            Keypad: "1B4F6B 1B4F326B 1B4F336B 1B4F346B 1B4F356B 1B4F366B 1B4F376B 1B4F386B"),
        new(Hex1bKey.Subtract, "2D 2D 1B2D 1B2D 2D 2D 1B2D 1B2D",
            Keypad: "1B4F6D 1B4F326D 1B4F336D 1B4F346D 1B4F356D 1B4F366D 1B4F376D 1B4F386D"),
        new(Hex1bKey.Decimal, "2E 2E 1B2E 1B2E 2E 2E 1B2E 1B2E",
            Keypad: "1B4F6E 1B4F326E 1B4F336E 1B4F346E 1B4F356E 1B4F366E 1B4F376E 1B4F386E"),
        new(Hex1bKey.Divide, "2F 2F 1B2F 1B2F 2F 2F 1B2F 1B2F",
            Keypad: "1B4F6F 1B4F326F 1B4F336F 1B4F346F 1B4F356F 1B4F366F 1B4F376F 1B4F386F"),
    ];

    public static IEnumerable<object[]> KeyboardCases
    {
        get
        {
            foreach (var vector in Vectors)
            foreach (var cursor in new[] { false, true })
            foreach (var keypad in new[] { false, true })
            {
                var expected = ((cursor ? vector.Cursor : null) ??
                    (keypad ? vector.Keypad : null) ?? vector.Normal).Split(' ');
                for (var modifier = 0; modifier < 8; modifier++)
                    yield return [vector.Key, (Hex1bModifiers)modifier, cursor, keypad, expected[modifier]];
            }
        }
    }

    [TestMethod]
    public void KeyboardCases_AllKeysModifiersAndModes_AreExplicitAndComplete()
    {
        TestSeq.AreEqual(Enum.GetValues<Hex1bKey>().Where(key => key != Hex1bKey.None).Order(),
            Vectors.Select(vector => vector.Key).Order());
        Assert.AreEqual(7, Enum.GetValues<Hex1bModifiers>().Aggregate(0, (mask, value) => mask | (int)value),
            "New modifiers require an explicit expansion of the wire matrix.");
        foreach (var vector in Vectors)
        foreach (var row in new[] { vector.Normal, vector.Cursor, vector.Keypad }.OfType<string>())
        {
            var cells = row.Split(' ');
            Assert.HasCount(8, cells, $"{vector.Key}: every modifier combination needs an expected value.");
            foreach (var cell in cells)
                Assert.IsNotEmpty(Convert.FromHexString(cell), $"{vector.Key}: a real key must not silently disappear.");
        }
        var cases = KeyboardCases.ToArray();
        Assert.HasCount(89 * 8 * 4, cases);
        Assert.AreEqual(cases.Length, cases.Select(row => (row[0], row[1], row[2], row[3])).Distinct().Count());
    }

    [TestMethod]
    [DynamicData(nameof(KeyboardCases))]
    public async Task Keyboard_AllAutomationPaths_TransmitExactBytes(
        Hex1bKey key, Hex1bModifiers modifiers, bool applicationCursor, bool applicationKeypad, string expectedHex)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            (applicationCursor ? "\u001b[?1h" : "\u001b[?1l") +
            (applicationKeypad ? "\u001b=" : "\u001b>")));
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));

        await automator.KeyAsync(key, modifiers, TestContext.Current.CancellationToken);
        workload.AssertNext(expectedHex, "automator explicit");
        await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
        workload.AssertNext("62", "automator explicit reset");

        if ((modifiers & Hex1bModifiers.Control) != 0) automator.Ctrl();
        if ((modifiers & Hex1bModifiers.Alt) != 0) automator.Alt();
        if ((modifiers & Hex1bModifiers.Shift) != 0) automator.Shift();
        await automator.KeyAsync(key, TestContext.Current.CancellationToken);
        workload.AssertNext(expectedHex, "automator fluent");
        await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
        workload.AssertNext("62", "automator fluent reset");

        for (var pending = 0; pending < 8; pending++)
        {
            var prefixes = (Hex1bModifiers)pending;
            if ((prefixes & modifiers) != prefixes) continue;
            if ((prefixes & Hex1bModifiers.Shift) != 0) automator.Shift().Shift();
            if ((prefixes & Hex1bModifiers.Alt) != 0) automator.Alt().Alt();
            if ((prefixes & Hex1bModifiers.Control) != 0) automator.Ctrl().Ctrl();
            await automator.KeyAsync(key, modifiers & ~prefixes, TestContext.Current.CancellationToken);
            workload.AssertNext(expectedHex, $"automator combined/duplicate modifiers ({prefixes})");
            await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
            workload.AssertNext("62", "automator combined reset");
        }

        var explicitSequence = new Hex1bTerminalInputSequenceBuilder()
            .Key(key, modifiers).Key(Hex1bKey.B).Build();
        using (await explicitSequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
        {
            workload.AssertNext(expectedHex, "sequence explicit");
            workload.AssertNext("62", "sequence explicit reset");
        }

        var builder = new Hex1bTerminalInputSequenceBuilder();
        if ((modifiers & Hex1bModifiers.Control) != 0) builder.Ctrl();
        if ((modifiers & Hex1bModifiers.Alt) != 0) builder.Alt();
        if ((modifiers & Hex1bModifiers.Shift) != 0) builder.Shift();
        using (await builder.Key(key).Key(Hex1bKey.B).Build().ApplyAsync(terminal, TestContext.Current.CancellationToken))
        {
            workload.AssertNext(expectedHex, "sequence fluent");
            workload.AssertNext("62", "sequence fluent reset");
        }

        for (var pending = 0; pending < 8; pending++)
        {
            var prefixes = (Hex1bModifiers)pending;
            if ((prefixes & modifiers) != prefixes) continue;
            builder = new Hex1bTerminalInputSequenceBuilder();
            if ((prefixes & Hex1bModifiers.Shift) != 0) builder.Shift().Shift();
            if ((prefixes & Hex1bModifiers.Alt) != 0) builder.Alt().Alt();
            if ((prefixes & Hex1bModifiers.Control) != 0) builder.Ctrl().Ctrl();
            using (await builder.Key(key, modifiers & ~prefixes).Key(Hex1bKey.B).Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            {
                workload.AssertNext(expectedHex, $"sequence combined/duplicate modifiers ({prefixes})");
                workload.AssertNext("62", "sequence combined reset");
            }
        }

        var step = TestSeq.IsType<KeyInputStep>(explicitSequence.Steps[0]);
        var input = new Hex1bKeyEvent(step.Key, step.Text, step.Modifiers);
        await terminal.SendEventAsync(input, TestContext.Current.CancellationToken);
        workload.AssertNext(expectedHex, "async event");
        terminal.SendEvent(input);
        await terminal.SendEventAsync(new Hex1bKeyEvent(Hex1bKey.B, "b", Hex1bModifiers.None),
            TestContext.Current.CancellationToken);
        workload.AssertNext(expectedHex, "sync event");
        workload.AssertNext("62", "sync event barrier");
        Assert.IsFalse(workload.Input.TryRead(out _), "Unexpected additional writes.");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Keyboard_AllKeysInOneSequence_PreservesByteOrder(bool applicationCursor, bool applicationKeypad)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            (applicationCursor ? "\u001b[?1h" : "\u001b[?1l") +
            (applicationKeypad ? "\u001b=" : "\u001b>")));
        var builder = new Hex1bTerminalInputSequenceBuilder();
        var expected = new List<byte>();
        foreach (var vector in Vectors)
        {
            var row = ((applicationCursor ? vector.Cursor : null) ??
                (applicationKeypad ? vector.Keypad : null) ?? vector.Normal).Split(' ');
            for (var modifier = 0; modifier < 8; modifier++)
            {
                builder.Key(vector.Key, (Hex1bModifiers)modifier);
                expected.AddRange(Convert.FromHexString(row[modifier]));
            }
        }

        using var snapshot = await builder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var actual = new List<byte>();
        while (workload.Input.TryRead(out var bytes))
            actual.AddRange(bytes);
        TestSeq.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task Keyboard_NoneSentinel_EmitsNothingAndConsumesModifiers()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        for (var modifier = 0; modifier < 8; modifier++)
        {
            await automator.KeyAsync(Hex1bKey.None, (Hex1bModifiers)modifier, TestContext.Current.CancellationToken);
            Assert.IsFalse(workload.Input.TryRead(out _));
            await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
            workload.AssertNext("62", "None consumed modifiers");
        }
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    public async Task Keyboard_ModeChangesAndRepeatedSequence_UseLiveModes()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        var sequence = new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.NumPad1).Build();

        foreach (var enabled in new[] { false, true, false })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(enabled ? "\u001b[?1h\u001b=" : "\u001b[?1l\u001b>"));
            await automator.UpAsync(TestContext.Current.CancellationToken);
            workload.AssertNext(enabled ? "1B4F41" : "1B5B41", "cached cursor key");
            using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
                workload.AssertNext(enabled ? "1B4F71" : "31", "replayed keypad key");
        }
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow("CAS")]
    [DataRow("CSA")]
    [DataRow("ACS")]
    [DataRow("ASC")]
    [DataRow("SCA")]
    [DataRow("SAC")]
    public async Task Keyboard_ModifierPrefixOrderAndOverlap_DoNotChangeEncoding(string order)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        var builder = new Hex1bTerminalInputSequenceBuilder();
        foreach (var modifier in order)
        {
            switch (modifier)
            {
                case 'C': automator.Ctrl(); builder.Ctrl(); break;
                case 'A': automator.Alt(); builder.Alt(); break;
                case 'S': automator.Shift(); builder.Shift(); break;
                default: Assert.Fail($"Unexpected modifier {modifier}."); break;
            }
        }

        var allModifiers = Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift;
        await automator.KeyAsync(Hex1bKey.E, allModifiers, TestContext.Current.CancellationToken);
        workload.AssertNext("1B05", "automator modifier order");
        using (await builder.Key(Hex1bKey.E, allModifiers).Build().ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertNext("1B05", "sequence modifier order");
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Keyboard_AppWorkload_KeypadTextIsIndependentOfTerminalMode(bool applicationKeypad)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(applicationKeypad ? "\u001b=" : "\u001b>"));
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        foreach (var vector in Vectors.Where(vector => vector.Keypad != null))
        for (var modifier = 0; modifier < 8; modifier++)
        {
            var expectedText = System.Text.Encoding.ASCII.GetString(Convert.FromHexString(vector.Normal.Split(' ')[0]));
            await automator.KeyAsync(vector.Key, (Hex1bModifiers)modifier, TestContext.Current.CancellationToken);
            Assert.IsTrue(workload.InputEvents.TryRead(out var input));
            Assert.AreEqual(new Hex1bKeyEvent(vector.Key, expectedText, (Hex1bModifiers)modifier), input);
        }
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    private sealed record KeyVector(Hex1bKey Key, string Normal, string? Cursor = null, string? Keypad = null);

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        internal ChannelReader<byte[]> Input => _input.Reader;

        internal void AssertNext(string expectedHex, string path)
        {
            Assert.IsTrue(Input.TryRead(out var bytes), $"{path}: no input was transmitted.");
            Assert.AreEqual(expectedHex, Convert.ToHexString(bytes), path);
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => _input.Writer.WriteAsync(data.ToArray(), ct);
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
