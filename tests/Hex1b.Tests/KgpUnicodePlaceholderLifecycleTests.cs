using Hex1b.Reflow;

namespace Hex1b.Tests;

public partial class KgpUnicodePlaceholderTests
{
    [TestMethod]
    [DataRow("overwrite")]
    [DataRow("el-to-end")]
    [DataRow("el-to-start")]
    [DataRow("el-all")]
    [DataRow("ed-to-end")]
    [DataRow("ed-to-start")]
    [DataRow("ech")]
    public void Placeholder_TextErasure_RemovesOnlyRealizedCell(
        string operation)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 4);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[2;3H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");
        TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);

        Apply(terminal, operation switch
        {
            "overwrite" => "\x1b[2;3HX",
            "el-to-end" => "\x1b[2;3H\x1b[K",
            "el-to-start" => "\x1b[2;3H\x1b[1K",
            "el-all" => "\x1b[2;3H\x1b[2K",
            "ed-to-end" => "\x1b[2;3H\x1b[J",
            "ed-to-start" => "\x1b[2;3H\x1b[1J",
            "ech" => "\x1b[2;3H\x1b[X",
            _ => throw new InvalidOperationException(operation),
        });

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
    }

    [TestMethod]
    [DataRow("insert", 3)]
    [DataRow("delete", 1)]
    public void Placeholder_InsertDeleteCharacters_MovesWithText(
        string operation,
        int expectedColumn)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 3);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[1;3H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, operation == "insert"
            ? "\x1b[1;2H\x1b[@"
            : "\x1b[1;2H\x1b[P");

        Assert.AreEqual(
            expectedColumn,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).Column);
    }

    [TestMethod]
    [DataRow("insert-line", 2)]
    [DataRow("delete-line", 0)]
    public void Placeholder_InsertDeleteLines_MovesWithText(
        string operation,
        int expectedRow)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 4);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[2;2H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, operation == "insert-line"
            ? "\x1b[1;1H\x1b[L"
            : "\x1b[1;1H\x1b[M");

        Assert.AreEqual(
            expectedRow,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).Row);
    }

    [TestMethod]
    public void Placeholder_DeferredWrap_MovesNextPlaceholderToNextRow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 3);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);

        Apply(terminal,
            "ABCD" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(0, placement.Column);
    }

    [TestMethod]
    public void Placeholder_FullScreenScroll_EntersAndLeavesScrollbackWithCell()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 1);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, "\x1b[S");

        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        using (var history = terminal.CreateSnapshot(scrollbackLines: 1))
        {
            var placement = TestSeq.Single(history.KgpPlacements);
            Assert.AreEqual(0, placement.Row);
            Assert.AreEqual(0, placement.Column);
        }

        Apply(terminal, "\x1b[S");

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.IsEmpty(terminal.CreateSnapshot(scrollbackLines: 1).KgpPlacements);
    }

    [TestMethod]
    public void Placeholder_PartialMarginsScroll_MovesOnlyCell()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 4);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[3;2H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m" +
            "\x1b[2;3r\x1b[S");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(1, placement.Column);
    }

    [TestMethod]
    public void Placeholder_HorizontalAndVerticalMarginsScroll_MovesContainedCell()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 4);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[?69h\x1b[2;4s\x1b[2;3r" +
            "\x1b[3;3H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[S");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(2, placement.Column);
    }

    [TestMethod]
    public void Placeholder_CropResize_DropsClippedCellButKeepsPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 3);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[1;5H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        terminal.Resize(3, 3);

        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
    }

    [TestMethod]
    public void Placeholder_BuiltInReflow_PreservesCellLineage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 3,
            scrollbackCapacity: 4,
            reflow: KittyReflowStrategy.Instance);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "A" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0mBCDE");

        terminal.Resize(2, 3);

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(1, placement.Column);

        terminal.Resize(3, 3);
        placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(1, placement.Column);
    }

    [TestMethod]
    public void Placeholder_MainAndAlternateScreens_IsolateSameIdentity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 3);
        AddVirtualImage(
            terminal, 42, 10, 20, columns: 1, rows: 1, fillByte: 0x11);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");
        using var mainBefore = terminal.CreateSnapshot();

        Apply(terminal, "\x1b[?1049h");
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        AddVirtualImage(
            terminal, 42, 20, 20, columns: 2, rows: 1, fillByte: 0x22);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");
        using var alternate = terminal.CreateSnapshot();
        Assert.IsTrue(alternate.InAlternateScreen);
        Assert.AreEqual(2u, TestSeq.Single(alternate.KgpPlacements).DisplayColumns);
        Assert.AreEqual(0x22, alternate.KgpImages[42].Data[0]);

        Apply(terminal, "\x1b[?1049l");

        using var restored = terminal.CreateSnapshot();
        Assert.IsFalse(restored.InAlternateScreen);
        Assert.AreEqual(1u, TestSeq.Single(restored.KgpPlacements).DisplayColumns);
        Assert.AreEqual(0x11, restored.KgpImages[42].Data[0]);
        Assert.AreEqual(0x11, mainBefore.KgpImages[42].Data[0]);
        Assert.AreEqual(0x22, alternate.KgpImages[42].Data[0]);
    }

    [TestMethod]
    public void Placeholder_RepeatedAndUnbalancedAlternateSwitches_ResetOnlyAlternate()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(
            terminal, 42, 10, 20, columns: 1, rows: 1, fillByte: 0x11);
        Apply(terminal, "\x1b[?1049l\x1b[?1049h");
        AddVirtualImage(
            terminal, 43, 10, 20, columns: 1, rows: 1, fillByte: 0x22);

        Apply(terminal, "\x1b[?1049h");
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);

        Apply(terminal, "\x1b[?1049l\x1b[?1049l");
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0x11, terminal.KgpImageStore.GetImageById(42)!.Data[0]);
    }

    [TestMethod]
    public void Placeholder_Ed2AndEd3EraseCellsButPreservePrototypeAndData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 5,
            height: 3,
            scrollbackCapacity: 2);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, "\x1b[2J");
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[S\x1b[3J");
        Assert.IsEmpty(terminal.CreateSnapshot(scrollbackLines: 2).KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
    }

    [TestMethod]
    public void Placeholder_AlternateEd3PreservesOnlyCurrentPrototypeUntilExit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(
            terminal, 42, 10, 20, columns: 1, rows: 1, fillByte: 0x11);
        Apply(terminal, "\x1b[?1049h");
        AddVirtualImage(
            terminal, 43, 10, 20, columns: 1, rows: 1, fillByte: 0x22);
        Apply(terminal,
            Foreground(43) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[3J");

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(43));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);

        Apply(terminal, "\x1b[?1049l");
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(43));
    }

    [TestMethod]
    public void Placeholder_RisClearsPrototypeCellsAndImageData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, "\x1b" + "c");

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void Placeholder_ImageReplacementRemovesPrototypeUntilRecreated()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(
            terminal, 42, 10, 20, columns: 1, rows: 1, fillByte: 0x11);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");
        using var before = terminal.CreateSnapshot();

        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42,
            10,
            20,
            KgpFormat.Rgb24,
            quiet: 2,
            fillByte: 0x22));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(0x11, before.KgpImages[42].Data[0]);

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,c=1,r=1,q=2"));

        using var after = terminal.CreateSnapshot();
        TestSeq.Single(after.KgpPlacements);
        Assert.AreEqual(0x22, after.KgpImages[42].Data[0]);
    }

    [TestMethod]
    public void Placeholder_InvalidExplicitReplacementStillRemovesOldPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=24,s=2,v=2,i=42,q=2",
            [1, 2, 3]));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(42));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public async Task Placeholder_ConcurrentReplacementAndSnapshot_AreGenerationPaired()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 2);

        void WriteGeneration(uint columns, byte fill)
        {
            var width = columns * 10;
            var text = Foreground(42) +
                Placeholder(row: 0, column: 0) +
                (columns == 2 ? Placeholder() : " ") +
                "\x1b[0m";
            Apply(
                terminal,
                "\x1b[H\x1b[2K" +
                KgpTestHelper.BuildCommand(
                    $"a=T,U=1,f=24,s={width},v=20,i=42,c={columns},r=1,q=2",
                    KgpTestHelper.CreatePixelData(
                        width,
                        20,
                        KgpFormat.Rgb24,
                        fill)) +
                text);
        }

        WriteGeneration(1, 0x11);
        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 200; i++)
                WriteGeneration((uint)(i % 2 + 1), i % 2 == 0 ? (byte)0x11 : (byte)0x22);
        });

        start.Set();
        try
        {
            for (var i = 0; i < 200; i++)
            {
                using var snapshot = terminal.CreateSnapshot();
                var placement = TestSeq.Single(snapshot.KgpPlacements);
                var image = snapshot.KgpImages[placement.ImageId];
                Assert.AreEqual(image.Width / 10, placement.DisplayColumns);
                Assert.AreEqual(
                    image.Width == 10 ? 0x11 : 0x22,
                    image.Data[0]);
                if (i % 16 == 0)
                    await Task.Yield();
            }
        }
        finally
        {
            await writer;
        }
    }

    [TestMethod]
    public void Placeholder_DisposeClearsBothScreenPrototypeOwners()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = CreateTerminal(workload);
        var mainStore = terminal.KgpImageStore;
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal, "\x1b[?1049h");
        var alternateStore = terminal.KgpImageStore;
        AddVirtualImage(terminal, 43, 10, 20, columns: 1, rows: 1);

        terminal.Dispose();

        Assert.AreEqual(0, mainStore.ImageCount);
        Assert.AreEqual(0, alternateStore.ImageCount);
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
    }

    [TestMethod]
    public async Task Placeholder_DisposeAsyncClearsBothScreenPrototypeOwners()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = CreateTerminal(workload);
        var mainStore = terminal.KgpImageStore;
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal, "\x1b[?1049h");
        var alternateStore = terminal.KgpImageStore;
        AddVirtualImage(terminal, 43, 10, 20, columns: 1, rows: 1);

        await terminal.DisposeAsync();

        Assert.AreEqual(0, mainStore.ImageCount);
        Assert.AreEqual(0, alternateStore.ImageCount);
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
    }
}
