using System.Net;
using WebTerminalDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
if (builder.Configuration["urls"] is null)
    builder.WebHost.UseUrls("http://localhost:5290");
var app = builder.Build();
var tapes = new DemoTapeCatalog(app.Environment.ContentRootPath);
await using var terminals = new TerminalRegistry(tapes, app.Logger, app.Lifetime.ApplicationStopping);

// This demo can launch a local shell. Reject remote clients, DNS rebinding, and cross-origin mutations.
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.Trim('[', ']');
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
        !(host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
          IPAddress.TryParse(host, out var hostAddress) && IPAddress.IsLoopback(hostAddress)))
    {
        await Results.Json(new { error = "Only loopback connections are allowed." }, statusCode: 403).ExecuteAsync(context);
        return;
    }
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    if ((context.Request.Path == "/ws" || !HttpMethods.IsGet(context.Request.Method) &&
         !HttpMethods.IsHead(context.Request.Method)) && !IsSameOrigin(context.Request))
    {
        await Results.Json(new { error = "A matching same-origin Origin header is required." }, statusCode: 403).ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ready", renderer = "WebGPU spike" }));
app.MapGet("/api/terminals", () => Results.Ok(terminals.List()));
app.MapPost("/api/terminals", (CreateTerminalRequest request) =>
{
    if (request.Scene is not ("mixed" or "text" or "sixel" or "kgp" or "animation" or "activity" or "shell"))
        return Results.BadRequest(new { error = "scene must be mixed, text, sixel, kgp, animation, activity, or shell." });
    if (request.Columns is < 20 or > 300 || request.Rows is < 10 or > 100)
        return Results.BadRequest(new { error = "columns must be 20..300 and rows must be 10..100." });
    if (!IsValidName(request.Name))
        return Results.BadRequest(new { error = "name must contain 1..80 printable characters when supplied." });
    if (!Enum.IsDefined(request.ReflowStrategy))
        return Results.BadRequest(new { error = "reflowStrategy must identify a supported reflow strategy." });
    try
    {
        var instance = terminals.Create(request);
        return instance is null
            ? Results.Json(new { error = "At most four terminal instances can run at once." }, statusCode: 429)
            : Results.Created($"/api/terminals/{instance.Id}", instance);
    }
    catch (ObjectDisposedException)
    {
        return Results.Json(new { error = "The application is shutting down." }, statusCode: 503);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not create terminal instance");
        return Results.Problem("Could not start the terminal workload; see server log.", statusCode: 500);
    }
});
app.MapDelete("/api/terminals/{id}", async (string id) =>
    await terminals.DeleteAsync(id)
        ? Results.NoContent()
        : Results.NotFound(new { error = "Terminal instance not found." }));
app.MapPost("/api/terminals/{id}/views/{viewId}/failure", (string id, string viewId, ViewFailureRequest request) =>
{
    var failure = BrowserCloseRequest.ForFailure(request.Mode);
    if (failure is null)
        return Results.BadRequest(new { error = "mode must be close, abort, policy, or server-error." });
    return terminals.RequestViewFailure(id, viewId, failure) switch
    {
        404 => Results.NotFound(new { error = "Terminal instance or browser view not found." }),
        409 => Results.Conflict(new { error = "This browser view is already closing." }),
        _ => Results.Accepted(value: new { status = "closing" })
    };
});
app.MapPost("/api/terminals/{id}/controls", (string id, TerminalControlsRequest request) =>
{
    if (request.Rate is < 1 or > 120 || request.Batch is < 1 or > 1000)
        return Results.BadRequest(new { error = "rate must be 1..120 and batch must be 1..1000." });
    return terminals.UpdateControls(id, request) switch
    {
        404 => Results.NotFound(new { error = "Terminal instance not found." }),
        409 => Results.Conflict(new { error = "Controls are only available for generated workloads, not shells." }),
        _ => Results.NoContent()
    };
});
app.MapPost("/api/terminals/{id}/tape", (string id, PlayTapeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.TapeId) || request.TapeId.Length > 80)
        return Results.BadRequest(new { error = "tapeId must identify a bundled tape for this terminal's scene." });
    return terminals.StartTape(id, request.TapeId) switch
    {
        404 => Results.NotFound(new { error = "Terminal instance not found." }),
        400 => Results.BadRequest(new { error = "This tape is not available for the terminal's scene." }),
        409 => Results.Conflict(new { error = "A tape is already playing on this terminal. Stop it before starting another." }),
        _ => Results.Accepted(value: new { status = "running" })
    };
});
app.MapDelete("/api/terminals/{id}/tape", (string id) => terminals.CancelTape(id) switch
{
    404 => Results.NotFound(new { error = "Terminal instance not found." }),
    409 => Results.Conflict(new { error = "No tape is playing on this terminal." }),
    _ => Results.Accepted(value: new { status = "cancelling" })
});
app.MapGet("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        await Results.BadRequest(new { error = "A WebSocket upgrade is required." }).ExecuteAsync(context);
        return;
    }
    var id = context.Request.Query["instance"].ToString();
    var name = context.Request.Query["name"].ToString();
    var transport = context.Request.Query["transport"].ToString();
    var viewId = context.Request.Query["view"].ToString();
    var failure = context.Request.Query["failure"].ToString() switch
    {
        "" => InitialViewFailure.None,
        "before-frame-close" => InitialViewFailure.BeforeFrameClose,
        "before-frame-abort" => InitialViewFailure.BeforeFrameAbort,
        "reject-upgrade" => InitialViewFailure.RejectUpgrade,
        _ => (InitialViewFailure?)null
    };
    if (failure is null)
    {
        await Results.BadRequest(new { error = "failure must be before-frame-close, before-frame-abort, or reject-upgrade." }).ExecuteAsync(context);
        return;
    }
    if (context.Request.Query.ContainsKey("view") &&
        (!Guid.TryParseExact(viewId, "D", out var parsedView) ||
         !parsedView.ToString("D").Equals(viewId, StringComparison.OrdinalIgnoreCase)))
    {
        await Results.BadRequest(new { error = "view must be a canonical GUID." }).ExecuteAsync(context);
        return;
    }
    if (transport is not ("" or "direct" or "hmp1"))
    {
        await Results.BadRequest(new { error = "transport must be direct or hmp1." }).ExecuteAsync(context);
        return;
    }
    if (string.IsNullOrWhiteSpace(id) || name.Length > 0 && !IsValidName(name))
    {
        await Results.BadRequest(new { error = "instance is required; name may contain 1..80 printable characters." }).ExecuteAsync(context);
        return;
    }
    var (view, status) = terminals.TryOpenView(id, string.IsNullOrEmpty(viewId) ? null : viewId);
    if (view is null)
    {
        await Results.Json(new { error = status switch
        {
            429 => "At most eight browser views can connect at once.",
            409 => "This view token is already connected.",
            _ => "Terminal instance not found or no longer running."
        } }, statusCode: status).ExecuteAsync(context);
        return;
    }
    using (view)
    {
        if (failure == InitialViewFailure.RejectUpgrade)
        {
            await Results.Json(new { error = "Demo: WebSocket upgrade rejected." }, statusCode: 503).ExecuteAsync(context);
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await new BrowserSession(socket, view, string.IsNullOrEmpty(name) ? null : name,
                transport == "hmp1", app.Logger, failure.Value)
            .RunAsync(context.RequestAborted);
    }
});
await app.RunAsync();

static bool IsSameOrigin(HttpRequest request) =>
    Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) &&
    origin.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
    origin.Scheme.Equals(request.Scheme, StringComparison.OrdinalIgnoreCase) &&
    origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0 &&
    origin.UserInfo.Length == 0;

static bool IsValidName(string? name) =>
    name is null || !string.IsNullOrWhiteSpace(name) && name.Length <= 80 && !name.Any(char.IsControl);
