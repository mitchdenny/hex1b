using Hex1b;
using Hex1b.Events;
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
IReadOnlyList<string> GetListItems() => contacts.Select(c => c.Name).ToList();

// State for the detail form - track the current edit values
var editName = "";
var editEmail = "";

// Track selection via callback
int selectedIndex = 0;

// Helper to get current contact
Contact? GetSelectedContact() => 
    selectedIndex >= 0 && selectedIndex < contacts.Count ? contacts[selectedIndex] : null;

// When selection changes, update the detail form
void OnSelectionChanged(ListSelectionChangedEventArgs args)
{
    selectedIndex = args.SelectedIndex;
    if (selectedIndex >= 0 && selectedIndex < contacts.Count)
    {
        var contact = contacts[selectedIndex];
        editName = contact.Name;
        editEmail = contact.Email;
    }
}

// Save action - updates the contact in the list
var statusMessage = "";
void Save()
{
    var contact = GetSelectedContact();
    if (contact != null)
    {
        contact.Name = editName;
        contact.Email = editEmail;
        statusMessage = $"Saved {contact.Name}";
    }
}

// Initialize with first contact
if (contacts.Count > 0)
{
    editName = contacts[0].Name;
    editEmail = contacts[0].Email;
}

// Create and run the app
using var app = new Hex1bApp(
    ctx => App(GetListItems, () => editName, n => editName = n, () => editEmail, e => editEmail = e, Save, () => statusMessage, OnSelectionChanged, cts, ctx.CancellationToken),
    new Hex1bAppOptions { Theme = Hex1bThemes.Sunset });
await app.RunAsync(cts.Token);

// The root component - master-detail layout with status bar
static Task<Hex1bWidget> App(
    Func<IReadOnlyList<string>> getListItems, 
    Func<string> getName,
    Action<string> setName,
    Func<string> getEmail,
    Action<string> setEmail,
    Action onSave,
    Func<string> getStatusMessage,
    Action<ListSelectionChangedEventArgs> onSelectionChanged,
    CancellationTokenSource cts, 
    CancellationToken cancellationToken)
{
    var ctx = new RootContext();
    
    var statusMessage = getStatusMessage();
    var instructions = "Tab: Next  |  Esc: Back  |  Ctrl+S: Save  |  Ctrl+Q: Quit";
    var statusText = string.IsNullOrEmpty(statusMessage) 
        ? instructions 
        : $"{statusMessage}  |  {instructions}";

    var widget = ctx.VStack(v => [
        v.Splitter(
            v.VStack(master => [
                master.List(getListItems(), onSelectionChanged, null)
            ]),
            v.VStack(detail => [
                detail.HStack(h => [h.Text("Name:  "), h.TextBox(getName(), args => setName(args.NewText))]),
                detail.HStack(h => [h.Text("Email: "), h.TextBox(getEmail(), args => setEmail(args.NewText))]),
                detail.Text(""),
                detail.Button("Save", _ => onSave()),
                detail.Button("Close", _ => cts.Cancel())
            ]),
            leftWidth: 25
        ).FillHeight().WithInputBindings(bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(_ => onSave(), "Save contact");
            bindings.Ctrl().Key(Hex1bKey.Q).Action(_ => cts.Cancel(), "Quit application");
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