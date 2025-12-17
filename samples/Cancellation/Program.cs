using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

// Set up cancellation with Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Sample data - mutable contact model
var contacts = new List<Contact>
{
    new("1", "Alice Johnson", "alice.johnson@example.com"),
    new("2", "Bob Smith", "bob.smith@example.com"),
    new("3", "Carol Williams", "carol.williams@example.com"),
    new("4", "David Brown", "david.brown@example.com"),
    new("5", "Eve Davis", "eve.davis@example.com"),
};

// Convert to list items for display
ListItem[] ToListItems() => contacts.Select(c => new ListItem(c.Id, c.Name)).ToArray();

// State for the list
var listState = new ListState { Items = ToListItems() };

// State for the detail form
var nameState = new TextBoxState();
var emailState = new TextBoxState();

// Helper to get current contact
Contact? GetSelectedContact() => 
    listState.SelectedItem is { } item ? contacts.Find(c => c.Id == item.Id) : null;

// When selection changes, update the detail form
listState.OnSelectionChanged = item =>
{
    var contact = contacts.Find(c => c.Id == item.Id);
    if (contact != null)
    {
        nameState.Text = contact.Name;
        nameState.CursorPosition = nameState.Text.Length;
        nameState.ClearSelection();
        emailState.Text = contact.Email;
        emailState.CursorPosition = emailState.Text.Length;
        emailState.ClearSelection();
    }
};

// Save action - updates the contact in the list
var statusMessage = "";
void Save()
{
    var contact = GetSelectedContact();
    if (contact != null)
    {
        contact.Name = nameState.Text;
        contact.Email = emailState.Text;
        // Refresh the list display
        listState.Items = ToListItems();
        statusMessage = $"Saved {contact.Name}";
    }
}

// Initialize with first contact
if (listState.SelectedItem != null)
{
    listState.OnSelectionChanged(listState.SelectedItem);
}

// Create and run the app
using var app = new Hex1bApp(
    ctx => App(listState, nameState, emailState, Save, () => statusMessage, cts, ctx.CancellationToken),
    new Hex1bAppOptions { Theme = Hex1bThemes.Sunset });
await app.RunAsync(cts.Token);

// The root component - master-detail layout with status bar
static Task<Hex1bWidget> App(
    ListState listState, 
    TextBoxState nameState, 
    TextBoxState emailState,
    Action onSave,
    Func<string> getStatusMessage,
    CancellationTokenSource cts, 
    CancellationToken cancellationToken)
{
    var ctx = new RootContext<object>(new object());
    
    var statusMessage = getStatusMessage();
    var instructions = "Tab: Next  |  Esc: Back  |  Ctrl+S: Save  |  Ctrl+Q: Quit";
    var statusText = string.IsNullOrEmpty(statusMessage) 
        ? instructions 
        : $"{statusMessage}  |  {instructions}";

    var widget = ctx.VStack(v => [
        v.Splitter(
            v.VStack(master => [
                master.List(listState)
            ]),
            v.VStack(detail => [
                detail.HStack(h => [h.Text("Name:  "), h.TextBox(nameState)]),
                detail.HStack(h => [h.Text("Email: "), h.TextBox(emailState)]),
                detail.Text(""),
                detail.Button("Save", onSave),
                detail.Button("Close", () => cts.Cancel())
            ]),
            leftWidth: 25
        ).FillHeight().WithInputBindings(bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(onSave, "Save contact");
            bindings.Ctrl().Key(Hex1bKey.Q).Action(() => cts.Cancel(), "Quit application");
        }),
        v.HStack(h => [h.Text(statusText)])
    ]);

    return Task.FromResult<Hex1bWidget>(widget);
}

// Mutable contact model
class Contact(string id, string name, string email)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
    public string Email { get; set; } = email;
}