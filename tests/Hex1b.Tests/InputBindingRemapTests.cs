using Hex1b.Input;

namespace Hex1b.Tests;

[TestClass]
public class InputBindingRemapTests
{
    private static readonly ActionId TestMoveUp = new("Test.MoveUp");
    private static readonly ActionId TestMoveDown = new("Test.MoveDown");
    private static readonly ActionId TestActivate = new("Test.Activate");
    private static readonly ActionId TestUnregistered = new("Test.Unregistered");

    [TestMethod]
    public void Triggers_SetsActionIdOnBinding()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        TestSeq.Single(builder.Bindings);
        Assert.AreEqual(TestMoveUp, builder.Bindings[0].ActionId);
        Assert.AreEqual("Move up", builder.Bindings[0].Description);
    }

    [TestMethod]
    public void Triggers_RegistersHandlerInRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        // Verify handler is registered by creating a rebinding
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        Assert.AreEqual(2, builder.Bindings.Count);
        Assert.AreEqual(TestMoveUp, builder.Bindings[1].ActionId);
    }

    [TestMethod]
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

        Assert.IsTrue(handlerCalled);
    }

    [TestMethod]
    public void Triggers_AsyncHandler_SetsActionIdAndRegisters()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp,
            _ => Task.CompletedTask, "Move up");

        Assert.AreEqual(TestMoveUp, builder.Bindings[0].ActionId);

        // Verify rebinding works
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);
        Assert.AreEqual(2, builder.Bindings.Count);
    }

    [TestMethod]
    public void Triggers_Unregistered_Throws()
    {
        var builder = new InputBindingsBuilder();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.Key(Hex1bKey.K).Triggers(TestUnregistered));
    }

    [TestMethod]
    public void Remove_ByActionId_RemovesAllMatchingBindings()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        builder.Remove(TestActivate);

        TestSeq.Single(builder.Bindings);
        Assert.AreEqual(TestMoveUp, builder.Bindings[0].ActionId);
    }

    [TestMethod]
    public void Remove_ByActionId_PreservesRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Remove(TestActivate);

        Assert.IsEmpty(builder.Bindings);

        // Handler still in registry — can rebind
        builder.Key(Hex1bKey.K).Triggers(TestActivate);
        TestSeq.Single(builder.Bindings);
        Assert.AreEqual(TestActivate, builder.Bindings[0].ActionId);
    }

    [TestMethod]
    public void Remove_ByActionId_RemovesMouseBindingsToo()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");

        builder.Remove(TestActivate);

        Assert.IsEmpty(builder.Bindings);
        Assert.IsEmpty(builder.MouseBindings);
    }

    [TestMethod]
    public void Remove_ByActionId_UnknownId_IsNoOp()
    {
        var builder = new InputBindingsBuilder();
        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");

        builder.Remove(TestUnregistered); // should not throw

        TestSeq.Single(builder.Bindings);
    }

    [TestMethod]
    public void RemoveAll_ClearsAllBindingTypes()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");
        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");
        builder.AnyCharacter().Action(_ => { }, "Type text");

        builder.RemoveAll();

        Assert.IsEmpty(builder.Bindings);
        Assert.IsEmpty(builder.MouseBindings);
        Assert.IsEmpty(builder.CharacterBindings);
    }

    [TestMethod]
    public void RemoveAll_PreservesRegistry()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.RemoveAll();

        // Can still rebind
        builder.Key(Hex1bKey.K).Triggers(TestActivate);
        TestSeq.Single(builder.Bindings);
    }

    [TestMethod]
    public void GetBindings_ReturnsMatchingBindings()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        var activateBindings = builder.GetBindings(TestActivate);
        Assert.AreEqual(2, activateBindings.Count);

        var moveUpBindings = builder.GetBindings(TestMoveUp);
        TestSeq.Single(moveUpBindings);
    }

    [TestMethod]
    public void GetBindings_UnknownId_ReturnsEmpty()
    {
        var builder = new InputBindingsBuilder();

        var result = builder.GetBindings(TestUnregistered);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void GetAllActionIds_ReturnsAllUniqueIds()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.Spacebar).Triggers(TestActivate, _ => { }, "Activate");
        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");
        builder.Key(Hex1bKey.DownArrow).Triggers(TestMoveDown, _ => { }, "Move down");

        var ids = builder.GetAllActionIds();
        Assert.AreEqual(3, ids.Count);
        Assert.Contains(TestActivate, ids);
        Assert.Contains(TestMoveUp, ids);
        Assert.Contains(TestMoveDown, ids);
    }

    [TestMethod]
    public void GetAllActionIds_Empty_ReturnsEmpty()
    {
        var builder = new InputBindingsBuilder();
        var ids = builder.GetAllActionIds();
        Assert.IsEmpty(ids);
    }

    [TestMethod]
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

        Assert.AreEqual(2, builder.Bindings.Count);
        Assert.AreEqual(Hex1bKey.K, builder.Bindings[0].Steps[0].Key);
        Assert.AreEqual(TestMoveUp, builder.Bindings[0].ActionId);
        Assert.AreEqual(Hex1bKey.J, builder.Bindings[1].Steps[0].Key);
        Assert.AreEqual(TestMoveDown, builder.Bindings[1].ActionId);
    }

    [TestMethod]
    public void AliasPattern_BindWithoutRemove_KeepsBoth()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.UpArrow).Triggers(TestMoveUp, _ => { }, "Move up");

        // Alias: just Bind (don't Remove)
        builder.Key(Hex1bKey.K).Triggers(TestMoveUp);

        Assert.AreEqual(2, builder.Bindings.Count);
        Assert.AreEqual(Hex1bKey.UpArrow, builder.Bindings[0].Steps[0].Key);
        Assert.AreEqual(Hex1bKey.K, builder.Bindings[1].Steps[0].Key);
        TestSeq.All(builder.Bindings, b => Assert.AreEqual(TestMoveUp, b.ActionId));
    }

    [TestMethod]
    public void MouseBinding_Triggers_SetsActionId()
    {
        var builder = new InputBindingsBuilder();

        builder.Mouse(MouseButton.Left).Triggers(TestActivate, _ => { }, "Click");

        TestSeq.Single(builder.MouseBindings);
        Assert.AreEqual(TestActivate, builder.MouseBindings[0].ActionId);
    }

    [TestMethod]
    public void Action_WithoutTriggers_HasNullActionId()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.Enter).Action(_ => { }, "Press enter");

        TestSeq.Single(builder.Bindings);
        Assert.IsNull(builder.Bindings[0].ActionId);
    }

    [TestMethod]
    public void ActionId_ToString_ReturnsValue()
    {
        var id = new ActionId("List.MoveUp");
        Assert.AreEqual("List.MoveUp", id.ToString());
    }

    [TestMethod]
    public void ActionId_Equality_WorksCorrectly()
    {
        var id1 = new ActionId("List.MoveUp");
        var id2 = new ActionId("List.MoveUp");
        var id3 = new ActionId("List.MoveDown");

        Assert.AreEqual(id1, id2);
        Assert.AreNotEqual(id1, id3);
    }

    [TestMethod]
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

        Assert.IsTrue(firstHandlerCalled);
        Assert.IsFalse(secondHandlerCalled);
    }

    [TestMethod]
    public void Triggers_WithModifiers_PreservesModifiers()
    {
        var builder = new InputBindingsBuilder();

        builder.Ctrl().Key(Hex1bKey.A).Triggers(TestActivate, _ => { }, "Select all");

        TestSeq.Single(builder.Bindings);
        Assert.AreEqual(Hex1bModifiers.Control, builder.Bindings[0].Steps[0].Modifiers);
        Assert.AreEqual(Hex1bKey.A, builder.Bindings[0].Steps[0].Key);
        Assert.AreEqual(TestActivate, builder.Bindings[0].ActionId);
    }

    [TestMethod]
    public void Triggers_WithGlobal_PreservesGlobalFlag()
    {
        var builder = new InputBindingsBuilder();

        builder.Key(Hex1bKey.F1).Global().Triggers(TestActivate, _ => { }, "Help");

        Assert.IsTrue(builder.Bindings[0].IsGlobal);
        Assert.AreEqual(TestActivate, builder.Bindings[0].ActionId);
    }

    private static InputBindingActionContext CreateMockContext()
    {
        return new InputBindingActionContext(
            focusRing: null!,
            requestStop: null,
            cancellationToken: CancellationToken.None);
    }
}
