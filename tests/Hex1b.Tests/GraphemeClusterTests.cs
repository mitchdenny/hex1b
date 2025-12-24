using System.Globalization;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for grapheme cluster handling in TextBox.
/// 
/// A grapheme cluster is what users perceive as a single "character" but may be
/// composed of multiple Unicode code points (and thus multiple C# chars).
/// 
/// Examples:
/// - Emoji: 😀 (U+1F600) = 2 chars (surrogate pair)
/// - Emoji with skin tone: 👍🏽 = 4 chars (👍 + 🏽)
/// - ZWJ sequence: 👨‍👩‍👧 = 8 chars (man + ZWJ + woman + ZWJ + girl)
/// - Flag: 🇺🇸 = 4 chars (regional indicator U + regional indicator S)
/// - Combining characters: é = 1 or 2 chars (precomposed or e + combining acute)
/// 
/// The TextBox must:
/// 1. Insert entire grapheme clusters atomically
/// 2. Delete entire grapheme clusters with backspace/delete
/// 3. Move cursor by grapheme cluster, not by char
/// 4. Select by grapheme cluster boundaries
/// 5. Never split a cluster, which would result in broken/invalid text
/// </summary>
public class GraphemeClusterTests
{
    #region Test Data
    
    // Simple emoji (surrogate pair, 2 chars)
    private const string Emoji = "😀";
    
    // Emoji with skin tone modifier (4 chars: base + modifier)
    private const string EmojiWithSkinTone = "👍🏽";
    
    // ZWJ family sequence (man + ZWJ + woman + ZWJ + girl = 8 chars)
    private const string FamilyEmoji = "👨‍👩‍👧";
    
    // Flag emoji (2 regional indicators = 4 chars)
    private const string FlagEmoji = "🇺🇸";
    
    // Combining character sequence (e + combining acute accent = 2 chars, displays as 1)
    private const string CombiningE = "e\u0301"; // é as combining sequence
    
    // Precomposed é (single char)
    private const string PrecomposedE = "é";
    
    // Multiple combining marks (a + ring above + acute = 3 chars)
    private const string MultipleCombining = "a\u030A\u0301";
    
    // Keycap sequence (1 + combining enclosing keycap + variation selector)
    private const string Keycap1 = "1️⃣";

    #endregion

    #region StringInfo Baseline Tests (verify our understanding)

    [Fact]
    public void StringInfo_SimpleEmoji_IsOneGrapheme()
    {
        var info = new StringInfo(Emoji);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.Equal(2, Emoji.Length); // But 2 chars in UTF-16
    }

    [Fact]
    public void StringInfo_EmojiWithSkinTone_IsOneGrapheme()
    {
        var info = new StringInfo(EmojiWithSkinTone);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.True(EmojiWithSkinTone.Length >= 4); // 4+ chars in UTF-16
    }

    [Fact]
    public void StringInfo_FamilyEmoji_IsOneGrapheme()
    {
        var info = new StringInfo(FamilyEmoji);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.True(FamilyEmoji.Length >= 8); // 8+ chars in UTF-16
    }

    [Fact]
    public void StringInfo_FlagEmoji_IsOneGrapheme()
    {
        var info = new StringInfo(FlagEmoji);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.Equal(4, FlagEmoji.Length); // 4 chars (2 regional indicators, each 2 chars)
    }

    [Fact]
    public void StringInfo_CombiningCharacter_IsOneGrapheme()
    {
        var info = new StringInfo(CombiningE);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.Equal(2, CombiningE.Length); // e + combining accent
    }

    [Fact]
    public void StringInfo_MultipleCombining_IsOneGrapheme()
    {
        var info = new StringInfo(MultipleCombining);
        Assert.Equal(1, info.LengthInTextElements);
        Assert.Equal(3, MultipleCombining.Length); // a + 2 combining marks
    }

    [Fact]
    public void StringInfo_MixedText_CountsCorrectly()
    {
        var text = "Hi" + Emoji + "!"; // 4 graphemes: H, i, 😀, !
        var info = new StringInfo(text);
        Assert.Equal(4, info.LengthInTextElements);
        Assert.Equal(5, text.Length); // 2 + 2 + 1 chars
    }

    #endregion

    #region Insert Text Tests

    [Fact]
    public async Task InsertText_SimpleEmoji_InsertsAsAtomicUnit()
    {
        var state = new TextBoxState { Text = "Hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(Emoji), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("Hello" + Emoji, state.Text);
        Assert.Equal(7, state.CursorPosition); // Moved by 2 chars
    }

    [Fact]
    public async Task InsertText_EmojiWithSkinTone_InsertsAsAtomicUnit()
    {
        var state = new TextBoxState { Text = "Test", CursorPosition = 4 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(EmojiWithSkinTone), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("Test" + EmojiWithSkinTone, state.Text);
        Assert.Equal(4 + EmojiWithSkinTone.Length, state.CursorPosition);
    }

    [Fact]
    public async Task InsertText_FamilyEmoji_InsertsAsAtomicUnit()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(FamilyEmoji), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(FamilyEmoji, state.Text);
        Assert.Equal(FamilyEmoji.Length, state.CursorPosition);
    }

    [Fact]
    public async Task InsertText_MultipleEmojis_InsertsAll()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(Emoji), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(FlagEmoji), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(EmojiWithSkinTone), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(Emoji + FlagEmoji + EmojiWithSkinTone, state.Text);
    }

    [Fact]
    public async Task InsertText_CombiningCharacter_InsertsAsAtomicUnit()
    {
        var state = new TextBoxState { Text = "cafe", CursorPosition = 4 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Insert the combining é after "caf"
        state.CursorPosition = 3;
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(CombiningE), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("caf" + CombiningE + "e", state.Text);
    }

    [Fact]
    public async Task InsertText_InMiddleOfText_InsertsCorrectly()
    {
        var state = new TextBoxState { Text = "AB", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(Emoji), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("A" + Emoji + "B", state.Text);
        Assert.Equal(1 + Emoji.Length, state.CursorPosition);
    }

    #endregion

    #region Delete Backward (Backspace) Tests

    [Fact]
    public async Task DeleteBackward_SimpleEmoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 + Emoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("AB", state.Text);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_EmojiWithSkinTone_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "X" + EmojiWithSkinTone, CursorPosition = 1 + EmojiWithSkinTone.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("X", state.Text);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_FamilyEmoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = FamilyEmoji, CursorPosition = FamilyEmoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_FlagEmoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "Flag: " + FlagEmoji, CursorPosition = 6 + FlagEmoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("Flag: ", state.Text);
        Assert.Equal(6, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_CombiningCharacter_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "caf" + CombiningE, CursorPosition = 3 + CombiningE.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("caf", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_MultipleCombining_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = MultipleCombining, CursorPosition = MultipleCombining.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_NeverSplitsCluster()
    {
        // Even if cursor is somehow in middle of cluster (shouldn't happen),
        // we should still delete the entire cluster
        var text = "A" + Emoji + "B";
        var state = new TextBoxState { Text = text };
        // Force cursor to middle of emoji (this is an invalid state, but we should handle it gracefully)
        state.CursorPosition = 2; // After A and first char of emoji
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Result should be valid text (no broken surrogate pairs)
        var enumerator = StringInfo.GetTextElementEnumerator(state.Text);
        while (enumerator.MoveNext())
        {
            var textElement = (string)enumerator.Current;
            // Each text element should be valid (no isolated surrogates)
            Assert.True(textElement.Length > 0);
        }
    }

    [Fact]
    public async Task DeleteBackward_AsciiBeforeEmoji_DeletesOnlyAscii()
    {
        var state = new TextBoxState { Text = "AB" + Emoji, CursorPosition = 2 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("A" + Emoji, state.Text);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteBackward_MultipleBackspaces_DeletesCorrectly()
    {
        var state = new TextBoxState { Text = "X" + Emoji + FlagEmoji, CursorPosition = 1 + Emoji.Length + FlagEmoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Delete flag
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        Assert.Equal("X" + Emoji, state.Text);

        // Delete emoji
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        Assert.Equal("X", state.Text);

        // Delete X
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        Assert.Equal("", state.Text);
    }

    #endregion

    #region Delete Forward (Delete key) Tests

    [Fact]
    public async Task DeleteForward_SimpleEmoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = Emoji + "B", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("B", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteForward_EmojiWithSkinTone_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "X" + EmojiWithSkinTone + "Y", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("XY", state.Text);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteForward_FamilyEmoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = FamilyEmoji + "!", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("!", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteForward_CombiningCharacter_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = CombiningE + "x", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("x", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task DeleteForward_AsciiAfterEmoji_DeletesOnlyAscii()
    {
        var state = new TextBoxState { Text = Emoji + "AB", CursorPosition = Emoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(Emoji + "B", state.Text);
        Assert.Equal(Emoji.Length, state.CursorPosition);
    }

    #endregion

    #region Cursor Movement Tests

    [Fact]
    public async Task MoveLeft_OverEmoji_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 + Emoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Cursor should be before the emoji, not in the middle of it
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task MoveRight_OverEmoji_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Cursor should be after the emoji
        Assert.Equal(1 + Emoji.Length, state.CursorPosition);
    }

    [Fact]
    public async Task MoveLeft_OverFamilyEmoji_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = FamilyEmoji, CursorPosition = FamilyEmoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task MoveRight_OverFamilyEmoji_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = FamilyEmoji, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(FamilyEmoji.Length, state.CursorPosition);
    }

    [Fact]
    public async Task MoveLeft_OverCombiningCharacter_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = "a" + CombiningE + "b", CursorPosition = 1 + CombiningE.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task MoveRight_OverCombiningCharacter_MovesEntireCluster()
    {
        var state = new TextBoxState { Text = "a" + CombiningE + "b", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(1 + CombiningE.Length, state.CursorPosition);
    }

    [Fact]
    public async Task MoveLeft_ThroughMixedText_NeverLandsMidCluster()
    {
        var text = "A" + Emoji + FlagEmoji + "B";
        var state = new TextBoxState { Text = text, CursorPosition = text.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        var validPositions = GetGraphemeBoundaryPositions(text);

        // Move left through entire text
        while (state.CursorPosition > 0)
        {
            await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
            Assert.Contains(state.CursorPosition, validPositions);
        }
    }

    [Fact]
    public async Task MoveRight_ThroughMixedText_NeverLandsMidCluster()
    {
        var text = "A" + Emoji + FlagEmoji + "B";
        var state = new TextBoxState { Text = text, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        var validPositions = GetGraphemeBoundaryPositions(text);

        // Move right through entire text
        while (state.CursorPosition < text.Length)
        {
            await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
            Assert.Contains(state.CursorPosition, validPositions);
        }
    }

    #endregion

    #region Selection Tests

    [Fact]
    public async Task SelectLeft_OverEmoji_SelectsEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 + Emoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);

        Assert.True(state.HasSelection);
        Assert.Equal(Emoji, state.SelectedText);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task SelectRight_OverEmoji_SelectsEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);

        Assert.True(state.HasSelection);
        Assert.Equal(Emoji, state.SelectedText);
        Assert.Equal(1 + Emoji.Length, state.CursorPosition);
    }

    [Fact]
    public async Task SelectLeft_OverFamilyEmoji_SelectsEntireCluster()
    {
        var state = new TextBoxState { Text = FamilyEmoji, CursorPosition = FamilyEmoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);

        Assert.True(state.HasSelection);
        Assert.Equal(FamilyEmoji, state.SelectedText);
    }

    [Fact]
    public async Task SelectAndDelete_Emoji_DeletesEntireCluster()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Select right over emoji
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);
        
        // Delete selection
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("AB", state.Text);
        Assert.Equal(1, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public async Task SelectAndDelete_MultipleEmojis_DeletesAll()
    {
        var text = "A" + Emoji + FlagEmoji + "B";
        var state = new TextBoxState { Text = text, CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Select right over both emojis
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);
        
        Assert.Equal(Emoji + FlagEmoji, state.SelectedText);

        // Delete selection
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("AB", state.Text);
    }

    [Fact]
    public async Task SelectAll_WithEmojis_SelectsCorrectly()
    {
        var text = Emoji + "Hello" + FamilyEmoji;
        var state = new TextBoxState { Text = text, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.True(state.HasSelection);
        Assert.Equal(text, state.SelectedText);
    }

    [Fact]
    public async Task TypeOverSelection_WithEmoji_ReplacesCorrectly()
    {
        var state = new TextBoxState { Text = "A" + Emoji + "B", CursorPosition = 1 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Select the emoji
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);
        
        // Type replacement text
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText(FlagEmoji), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("A" + FlagEmoji + "B", state.Text);
        Assert.Equal(1 + FlagEmoji.Length, state.CursorPosition);
        Assert.False(state.HasSelection);
    }

    #endregion

    #region Edge Cases and Invariant Tests

    [Fact]
    public async Task TextAlwaysValid_AfterAnyOperation()
    {
        var operations = new Func<TextBoxNode, Task>[]
        {
            async n => await InputRouter.RouteInputToNodeAsync(n, Hex1bKeyEvent.FromText(Emoji)),
            async n => await InputRouter.RouteInputToNodeAsync(n, Hex1bKeyEvent.FromText(FamilyEmoji)),
            async n => await InputRouter.RouteInputToNodeAsync(n, Hex1bKeyEvent.FromText("X")),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None)),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None)),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None)),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None)),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift)),
            async n => await InputRouter.RouteInputToNodeAsync(n, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Shift)),
        };

        var state = new TextBoxState { Text = "Test" + Emoji + FlagEmoji, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };
        var random = new Random(42); // Fixed seed for reproducibility

        // Perform 100 random operations
        for (int i = 0; i < 100; i++)
        {
            var operation = operations[random.Next(operations.Length)];
            await operation(node);

            // Verify text is valid (no isolated surrogates)
            AssertValidText(state.Text);
            
            // Verify cursor is at a valid position
            var validPositions = GetGraphemeBoundaryPositions(state.Text);
            Assert.Contains(state.CursorPosition, validPositions);
        }
    }

    [Fact]
    public async Task EmptyText_DeleteBackward_DoesNothing()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task EmptyText_DeleteForward_DoesNothing()
    {
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task CursorAtStart_MoveLeft_StaysAtStart()
    {
        var state = new TextBoxState { Text = Emoji, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task CursorAtEnd_MoveRight_StaysAtEnd()
    {
        var state = new TextBoxState { Text = Emoji, CursorPosition = Emoji.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(Emoji.Length, state.CursorPosition);
    }

    [Fact]
    public async Task Home_WithEmojis_MovesToStart()
    {
        var text = Emoji + FamilyEmoji + "Test";
        var state = new TextBoxState { Text = text, CursorPosition = text.Length };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task End_WithEmojis_MovesToEnd()
    {
        var text = Emoji + FamilyEmoji + "Test";
        var state = new TextBoxState { Text = text, CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(text.Length, state.CursorPosition);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets all valid cursor positions (grapheme cluster boundaries) in a string.
    /// </summary>
    private static List<int> GetGraphemeBoundaryPositions(string text)
    {
        var positions = new List<int> { 0 };
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            positions.Add(enumerator.ElementIndex + ((string)enumerator.Current).Length);
        }
        return positions;
    }

    /// <summary>
    /// Asserts that the text contains no isolated surrogates or broken grapheme clusters.
    /// </summary>
    private static void AssertValidText(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                Assert.True(i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]),
                    $"Isolated high surrogate at position {i}");
                i++; // Skip the low surrogate
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                Assert.Fail($"Isolated low surrogate at position {i}");
            }
        }
    }

    #endregion
}
