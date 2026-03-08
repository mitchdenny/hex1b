using Hex1b.Input;

namespace Hex1b.Tests;

public class InputBindingRemapTests
{
    private static readonly ActionId TestMoveUp = new("Test.MoveUp");
    private static readonly ActionId TestMoveDown = new("Test.MoveDown");
    private static readonly ActionId TestActivate = new("Test.Activate");
    private static readonly ActionId TestUnregistered = new("Test.Unregistered");

    [Fact]
    public void Triggers_SetsActionIdOnBinding()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        Assert.Single(builder.Bindings);
        Assert.Equal(TestMoveUp, builder.Bindings[0].ActionId);
        Assert.Equal("Move up", builder.Bindings[0].Description);
    }

    [Fact]
    public void Triggers_RegistersHandlerInRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        // Verify handler is registered by creating a rebinding
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        Assert.Equal(2, builder.Bindings.Count);
        Assert.Equal(TestMoveUp, builder.Bindings[1].ActionId);
    }

    [Fact]
    public async Task Triggers_RebindingResolvesCorrectHandler()
    {
        var builder = new InputBindingsBuilder();
        var handlerCalled = false;

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ =>
        {
            handlerCalled = true;
        }, "Move up");

        // Rebind to K
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        // Execute the rebinding's handler
        var context = CreateMockContext();
        await builder.Bindings[1].ExecuteAsync(context);

        Assert.True(handlerCalled);
    }

    [Fact]
    public void Triggers_AsyncHandler_SetsActionIdAndRegisters()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp,
            _ => Task.CompletedTask, "Move up");

        Assert.Equal(TestMoveUp, builder.Bindings[0].ActionId);

        // Verify rebinding works
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);
        Assert.Equal(2, builder.Bindings.Count);
    }

    [Fact]
    public void Triggers_Unregistered_Throws()
    {
        var builder = new InputBindingsBuilder();

        Assert.Throws<InvalidOperationException>(() =>
            builder.Key(Hex1bKey.K).Triggers(TestUnregistered));
    }

    [Fact]
    public void Remove_ByActionId_RemovesAllMatchingBindings()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        builder.Remove(TestActivate);

        Assert.Single(builder.Bindings);
        Assert.Equal(TestMoveUp, builder.Bindings[0].ActionId);
    }

    [Fact]
    public void Remove_ByActionId_PreservesRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Remove(TestActivate);

        Assert.Empty(builder.Bindings);

        // Handler still in registry — can rebind
        builder.Key(Hex1bKey.K).Triggers(TestActivate);
        Assert.Single(builder.Bindings);
        Assert.Equal(TestActivate, builder.Bindings[0].ActionId);
    }

    [Fact]
    public void Remove_ByActionId_RemovesMouseBindingsToo()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");

        builder.Remove(TestActivate);

        Assert.Empty(builder.Bindings);
        Assert.Empty(builder.MouseBindings);
    }

    [Fact]
    public void Remove_ByActionId_UnknownId_IsNoOp()
    {
        var builder = new InputBindingsBuilder();
        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");

        builder.Remove(TestUnregistered); // should not throw

        Assert.Single(builder.Bindings);
    }

    [Fact]
    public void RemoveAll_ClearsAllBindingTypes()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");
        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");
        builder.AnyCharacter().Action(_ => { }, "Type text");

        builder.RemoveAll();

        Assert.Empty(builder.Bindings);
        Assert.Empty(builder.MouseBindings);
        Assert.Empty(builder.CharacterBindings);
    }

    [Fact]
    public void RemoveAll_PreservesRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.RemoveAll();

        // Can still rebind
        builder.Key(Hex1bKey.K).Triggers(TestActivate);
        Assert.Single(builder.Bindings);
    }

    [Fact]
    public void GetBindings_ReturnsMatchingBindings()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        var activateBindings = builder.GetBindings(TestActivate);
        Assert.Equal(2, activateBindings.Count);

        var moveUpBindings = builder.GetBindings(TestMoveUp);
        Assert.Single(moveUpBindings);
    }

    [Fact]
    public void GetBindings_UnknownId_ReturnsEmpty()
    {
        var builder = new InputBindingsBuilder();

        var result = builder.GetBindings(TestUnregistered);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllActionIds_ReturnsAllUniqueIds()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");
        builder.Key(Hex1bKey.DownArrow).Triggers(TestMoveDown, _ => { }, "Move down");

        var ids = builder.GetAllActionIds();
        Assert.Equal(3, ids.Count);
        Assert.Contains(TestActivate, ids);
        Assert.Contains(TestMoveUp, ids);
        Assert.Contains(TestMoveDown, ids);
    }

    [Fact]
    public void GetAllActionIds_Empty_ReturnsEmpty()
    {
        var builder = new InputBindingsBuilder();
        var ids = builder.GetAllActionIds();
        Assert.Empty(ids);
    }

    [Fact]
    public void RemapPattern_RemoveThenBind_Works()
    {
        var builder = new InputBindingsBuilder();

        // Default bindings
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");
        builder.Key(Hex1bKey.DownArrow).Triggers(TestMoveDown, _ => { }, "Move down");

        // Remap: Remove + Bind
        builder.Remove(TestMoveUp);
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        builder.Remove(TestMoveDown);
        builder.Key(Hex1bKey.J).Triggers(TestMoveDown);

        Assert.Equal(2, builder.Bindings.Count);
        Assert.Equal(Hex1bKey.K, builder.Bindings[0].Steps[0].Key);
        Assert.Equal(TestMoveUp, builder.Bindings[0].ActionId);
        Assert.Equal(Hex1bKey.J, builder.Bindings[1].Steps[0].Key);
        Assert.Equal(TestMoveDown, builder.Bindings[1].ActionId);
    }

    [Fact]
    public void AliasPattern_BindWithoutRemove_KeepsBoth()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        // Alias: just Bind (don't Remove)
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        Assert.Equal(2, builder.Bindings.Count);
        Assert.Equal(Hex1bKey.UpArrow, builder.Bindings[0].Steps[0].Key);
        Assert.Equal(Hex1bKey.K, builder.Bindings[1].Steps[0].Key);
        Assert.All(builder.Bindings, b => Assert.Equal(TestMoveUp, b.ActionId));
    }

    [Fact]
    public void MouseBinding_Triggers_SetsActionId()
    {
        var builder = new InputBindingsBuilder();

        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");

        Assert.Single(builder.MouseBindings);
        Assert.Equal(TestActivate, builder.MouseBindings[0].ActionId);
    }

    [Fact]
    public void Action_WithoutTriggers_HasNullActionId()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Action(_ => { }, "Press enter");

        Assert.Single(builder.Bindings);
        Assert.Null(builder.Bindings[0].ActionId);
    }

    [Fact]
    public void ActionId_ToString_ReturnsValue()
    {
        var id = new ActionId("List.MoveUp");
        Assert.Equal("List.MoveUp", id.ToString());
    }

    [Fact]
    public void ActionId_Equality_WorksCorrectly()
    {
        var id1 = new ActionId("List.MoveUp");
        var id2 = new ActionId("List.MoveUp");
        var id3 = new ActionId("List.MoveDown");

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
    }

    [Fact]
    public void Triggers_FirstRegistrationWins()
    {
        var builder = new InputBindingsBuilder();
        var firstHandlerCalled = false;
        var secondHandlerCalled = false;

        // Register Activate with Enter (first handler)
        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { firstHandlerCalled = true; }, "Activate");

        // Register Activate with Spacebar (second handler — should NOT override first)
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { secondHandlerCalled = true; }, "Activate");

        // Rebind — should use the first registered handler
        builder.Key(Hex1bKey.K).Triggers(TestActivate);

        // Execute the rebinding's handler
        var context = CreateMockContext();
        builder.Bindings[2].ExecuteAsync(context).Wait();

        Assert.True(firstHandlerCalled);
        Assert.False(secondHandlerCalled);
    }

    [Fact]
    public void Triggers_WithModifiers_PreservesModifiers()
    {
        var builder = new InputBindingsBuilder();

        builder.Ctrl().Key(Hex1bKey.A).Triggers(TestActivate, _ => { }, "Select all");

        Assert.Single(builder.Bindings);
        Assert.Equal(Hex1bModifiers.Control, builder.Bindings[0].Steps[0].Modifiers);
        Assert.Equal(Hex1bKey.A, builder.Bindings[0].Steps[0].Key);
        Assert.Equal(TestActivate, builder.Bindings[0].ActionId);
    }

    [Fact]
    public void Triggers_WithGlobal_PreservesGlobalFlag()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.F1).Global().Triggers(TestActivate, _ => { }, "Help");

        Assert.True(builder.Bindings[0].IsGlobal);
        Assert.Equal(TestActivate, builder.Bindings[0].ActionId);
    }

    private static InputBindingActionContext CreateMockContext()
    {
        return new InputBindingActionContext(
            focusRing: null!,
            requestStop: null,
            cancellationToken: CancellationToken.None);
    }
}
