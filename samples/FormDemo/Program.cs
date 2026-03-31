using Hex1b;
using Hex1b.Data;
using Hex1b.Theming;
using Hex1b.Widgets;
using FormDemo;

// ──────────────────────────────────────────────────────
// FormDemo — Form widget + live map geocoding
// ──────────────────────────────────────────────────────

var firstName = "";
var lastName = "";
var email = "";
var company = "";
var title = "";
var address = "";
var city = "";
var state = "";
var postcode = "";
var lastAction = "None";
var labelPlacementIndex = 0; // 0 = Above, 1 = Inline
var geocodeStatus = "";

// Map infrastructure
var camera = new MapCamera(latitude: 40.7484, longitude: -73.9857, zoomLevel: 14);
using var tileClient = new RasterTileClient();
var dataSource = new OsmTileDataSource(tileClient, camera);
using var nominatim = new NominatimClient();
var mapPois = new List<TilePointOfInterest>();

// Builds a full address string from form fields for geocoding
string BuildAddressQuery()
{
    var parts = new List<string>();
    var addressLine = address.Replace("\n", " ").Trim();
    if (!string.IsNullOrEmpty(addressLine)) parts.Add(addressLine);
    if (!string.IsNullOrEmpty(city)) parts.Add(city);
    if (!string.IsNullOrEmpty(state)) parts.Add(state);
    if (!string.IsNullOrEmpty(postcode)) parts.Add(postcode);
    return string.Join(", ", parts);
}

// Fire-and-forget geocoding with debounce
void TriggerGeocode()
{
    var query = BuildAddressQuery();
    if (string.IsNullOrWhiteSpace(query))
    {
        geocodeStatus = "";
        return;
    }

    geocodeStatus = "Searching…";

    _ = Task.Run(async () =>
    {
        var result = await nominatim.GeocodeAsync(query);
        if (result is not null)
        {
            camera.Latitude = result.Latitude;
            camera.Longitude = result.Longitude;
            geocodeStatus = $"📍 {result.DisplayName[..Math.Min(result.DisplayName.Length, 60)]}";

            var (tileX, tileY) = TileCoordinates.LatLonToTile(result.Latitude, result.Longitude, camera.ZoomLevel);
            mapPois =
            [
                new TilePointOfInterest(tileX * 256, tileY * 128, "📍", "Address")
            ];
        }
        else
        {
            geocodeStatus = "No results";
        }
    });
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var labelPlacement = labelPlacementIndex == 0
            ? LabelPlacement.Above
            : LabelPlacement.Inline;

        var (cx, cy) = camera.CharCenter;

        return ctx.HSplitter(
            // ── Left: Form Panel ──
            formCol => [
                formCol.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                    formCol.Text("  ◆ Form Widget Demo")),
                formCol.Text("  Tab/Shift+Tab to navigate. Ctrl+C to exit."),
                formCol.Separator(),

                // Label placement toggle
                formCol.HStack(toggle => [
                    toggle.Text("  Labels: "),
                    toggle.ToggleSwitch(["Above", "Inline"], labelPlacementIndex)
                        .OnSelectionChanged(e => labelPlacementIndex = e.SelectedIndex),
                ]).ContentHeight(),

                formCol.Separator(),

                // ── Contact Form ──
                formCol.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
                    formCol.Text("  Contact Information")),
                formCol.Text(""),
                formCol.Form(form =>
                {
                    var firstNameField = form.TextField("First Name")
                        .WithWidth(20)
                        .Validate(value => string.IsNullOrWhiteSpace(value)
                            ? ValidationResult.Error("First name is required")
                            : ValidationResult.Valid)
                        .OnTextChanged(e => firstName = e.NewText);

                    var lastNameField = form.TextField("Last Name")
                        .WithWidth(20)
                        .Validate(value => string.IsNullOrWhiteSpace(value)
                            ? ValidationResult.Error("Last name is required")
                            : ValidationResult.Valid)
                        .OnTextChanged(e => lastName = e.NewText);

                    var emailField = form.TextField("Email")
                        .WithWidth(20)
                        .Validate(value =>
                        {
                            if (string.IsNullOrWhiteSpace(value))
                                return ValidationResult.Error("Email is required");
                            if (!value.Contains('@') || !value.Contains('.'))
                                return ValidationResult.Error("Enter a valid email");
                            return ValidationResult.Valid;
                        })
                        .Adornment(
                            async (value, ct) => value.Contains('@') && value.Contains('.'),
                            () => new IconWidget(" ✉"))
                        .OnTextChanged(e => email = e.NewText);

                    var companyField = form.TextField("Company")
                        .WithWidth(20)
                        .OnTextChanged(e => company = e.NewText);

                    var titleField = form.TextField("Job Title")
                        .WithWidth(20)
                        .OnTextChanged(e => title = e.NewText);

                    var addressField = form.TextField("Address")
                        .WithWidth(30)
                        .Multiline(2)
                        .WithHeight(2)
                        .OnTextChanged(e => { address = e.NewText; TriggerGeocode(); });

                    var cityField = form.TextField("City")
                        .WithWidth(20)
                        .OnTextChanged(e => { city = e.NewText; TriggerGeocode(); });

                    var stateField = form.TextField("State")
                        .WithWidth(15)
                        .OnTextChanged(e => { state = e.NewText; TriggerGeocode(); });

                    var postcodeField = form.TextField("Postcode")
                        .WithWidth(10)
                        .Validate(value =>
                        {
                            if (!string.IsNullOrEmpty(value) && !value.All(char.IsDigit))
                                return ValidationResult.Error("Digits only");
                            return ValidationResult.Valid;
                        })
                        .OnTextChanged(e => { postcode = e.NewText; TriggerGeocode(); });

                    return [
                        firstNameField,
                        lastNameField,
                        emailField,
                        companyField,
                        titleField,
                        form.Text(""),
                        form.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
                            form.Text("  Address")),
                        addressField,
                        cityField,
                        stateField,
                        postcodeField,
                        form.Text(""),
                        form.ValidationSummary(),
                        form.SubmitButton("Submit", _ => lastAction = "Submitted!"),
                        form.CancelButton(_ => lastAction = "Cancelled"),
                    ];
                }).WithLabelPlacement(labelPlacement)
                  .WithLabelWidth(15),

                formCol.Text(""),
                formCol.Separator(),

                // Live preview
                formCol.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                    formCol.VStack(preview => [
                        preview.Text($"  Name:    {firstName} {lastName}"),
                        preview.Text($"  Email:   {email}"),
                        preview.Text($"  Company: {company}"),
                        preview.Text($"  Title:   {title}"),
                        preview.Text($"  Address: {address.Replace("\n", ", ")}"),
                        preview.Text($"  City:    {city}  State: {state}  Post: {postcode}"),
                    ])),
                formCol.Text($"  Last action: {lastAction}"),
            ],

            // ── Right: Map Panel ──
            mapCol => [
                mapCol.HStack(header => [
                    header.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                        header.Text(" 🗺️ Map")),
                    header.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                        header.Text($" ({camera.Latitude:F4}, {camera.Longitude:F4}) z{camera.ZoomLevel} ")),
                ]).ContentHeight(),
                mapCol.TilePanel(dataSource, cx, cy, 0)
                    .WithPointsOfInterest(mapPois)
                    .OnPan(e => camera.Pan(e.DeltaX, e.DeltaY))
                    .OnZoom(e =>
                    {
                        camera.Zoom(e.Delta);
                        dataSource.ClearDecodedCache();
                    }),
                mapCol.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                    mapCol.Text($" {geocodeStatus}")).ContentHeight(),
            ],
            leftWidth: 50);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();
