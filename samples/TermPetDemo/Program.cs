using System.Diagnostics;
using System.Reflection;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;

const int MCols = 4, MRows = 3, NP = MCols * MRows;
const double BaseSpX = 1.3, BaseSpY = 1.8;
const double Stiff = 68.0;
const double SpringDamp = 8.0;
const double FreeDamp = 0.985;
const double EdgeDamp = 0.945;
const double Grav = 25.0;
const double EdgeStick = 13.0;
const double CrawlF = 1.0;
const double CrawlCadence = 1.15;
const double CrawlLag = 1.0;
const double CrawlLift = 0.9;
const double ReachRange = 7.5;
const double ReachPull = 10.5;
const double ReachSquirm = 0.7;
const double BlobR = 0.95;
const double Thresh = 1.0;
const int EyeIdx = 6;
const double GrabR2 = 9.0;
const double MaxFlickSpeed = 110.0;
const double FlickBoost = 0.95;
const double SplitShakeThreshold = 165.0;
const double MergeDistance = 4.8;
const int MinSplitPieces = 2;
const int MaxSplitPieces = 3;

var rng = new Random();
var lk = new object();
var scrW = 80;
var scrH = 24;

var sprA = Array.Empty<int>();
var sprB = Array.Empty<int>();
var sprR = Array.Empty<double>();

var eyePupil = Hex1bColor.FromRgb(30, 30, 30);
var baseGreen = new Rgb(60, 200, 60);
var mouseXField = typeof(Hex1bApp).GetField("_mouseX", BindingFlags.Instance | BindingFlags.NonPublic);
var mouseYField = typeof(Hex1bApp).GetField("_mouseY", BindingFlags.Instance | BindingFlags.NonPublic);

var animationClock = Stopwatch.StartNew();
var lastAnimationAt = animationClock.Elapsed;
var nextBlobId = 0;
var blobs = new List<SlugBlob>();

BuildSpringTopology();
blobs.Add(CreateBlob(40.0, 3.0, 1.0, baseGreen, baseGreen));

Hex1bApp? theApp = null;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var bash = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("/bin/bash")
    .WithTerminalWidget(out var bashHandle)
    .Build();

_ = Task.Run(async () =>
{
    try { await bash.RunAsync(cts.Token); }
    catch (OperationCanceledException) { }
    finally { cts.Cancel(); }
});

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, _) =>
    {
        theApp = app;
        return ctx =>
        {
            var term = ctx.Terminal(bashHandle)
                .WhenNotRunning(_ => ctx.Text(""))
                .WithInputBindings(bindings =>
                {
                    bindings.Drag(MouseButton.Left).Action((sx, sy) =>
                    {
                        var visX = sx + 0.5;
                        var visY = sy * 2.0 + 1.0;

                        SlugBlob? grabbedBlob;
                        int grabbedPoint;

                        lock (lk)
                        {
                            if (!TryFindNearestBlobPoint(visX, visY, out grabbedBlob, out grabbedPoint))
                            {
                                return new DragHandler();
                            }

                            var activeBlob = grabbedBlob!;
                            activeBlob.Grabbed = grabbedPoint;
                            activeBlob.GrabX = visX;
                            activeBlob.GrabY = visY;
                            activeBlob.DragVelX = 0;
                            activeBlob.DragVelY = 0;
                            activeBlob.LastDragX = visX;
                            activeBlob.LastDragY = visY;
                            activeBlob.LastDragAt = Stopwatch.GetTimestamp();
                            activeBlob.OnEdge = false;
                            activeBlob.FreeFlightUntil = animationClock.Elapsed + TimeSpan.FromMilliseconds(120);
                            activeBlob.ShakeEnergy = 0;
                        }

                        return new DragHandler(
                            onMove: (_, dx, dy) =>
                            {
                                lock (lk)
                                {
                                    var nextX = sx + dx + 0.5;
                                    var nextY = (sy + dy) * 2.0 + 1.0;
                                    UpdateDrag(grabbedBlob!, nextX, nextY);
                                    MaybeExplode(grabbedBlob!);
                                }

                                app.Invalidate();
                            },
                            onEnd: _ =>
                            {
                                lock (lk)
                                {
                                    ApplyFlickImpulse(grabbedBlob!);
                                    grabbedBlob!.Grabbed = -1;
                                }

                                app.Invalidate();
                            }
                        );
                    });
                })
                .Fill();

            return ctx.EffectPanel(term, surface =>
            {
                lock (lk)
                {
                    scrW = surface.Width;
                    scrH = surface.Height;

                    var now = animationClock.Elapsed;
                    var dt = Math.Clamp((now - lastAnimationAt).TotalSeconds, 1.0 / 120.0, 1.0 / 30.0);
                    lastAnimationAt = now;

                    UpdateWorld(dt, now);
                }

                RenderSlugs(surface);
            })
            .RedrawAfter(16)
            .Fill();
        };
    })
    .WithMouse()
    .Build();

await terminal.RunAsync(cts.Token);
return;

void BuildSpringTopology()
{
    var px = new double[NP];
    var py = new double[NP];

    var ox = -((MCols - 1) * BaseSpX / 2.0);
    var oy = -((MRows - 1) * BaseSpY / 2.0);

    for (int r = 0; r < MRows; r++)
    for (int c = 0; c < MCols; c++)
    {
        var i = r * MCols + c;
        px[i] = ox + c * BaseSpX;
        py[i] = oy + r * BaseSpY;
    }

    var sa = new List<int>();
    var sb = new List<int>();
    var sr = new List<double>();

    void Link(int a, int b)
    {
        var dx = px[a] - px[b];
        var dy = py[a] - py[b];
        sa.Add(a);
        sb.Add(b);
        sr.Add(Math.Sqrt(dx * dx + dy * dy));
    }

    for (int r = 0; r < MRows; r++)
    for (int c = 0; c < MCols; c++)
    {
        var i = r * MCols + c;
        if (c + 1 < MCols) Link(i, i + 1);
        if (r + 1 < MRows) Link(i, i + MCols);
        if (c + 1 < MCols && r + 1 < MRows)
        {
            Link(i, i + MCols + 1);
            Link(i + 1, i + MCols);
        }
    }

    sprA = sa.ToArray();
    sprB = sb.ToArray();
    sprR = sr.ToArray();
}

SlugBlob CreateBlob(double cx, double cy, double scale, Rgb startBody, Rgb targetBody)
{
    var blob = new SlugBlob
    {
        Id = nextBlobId++,
        Scale = scale,
        TargetScale = scale,
        CurrentBody = startBody,
        TargetBody = targetBody,
        CurrentOutline = Darken(startBody, 0.46),
        TargetOutline = Darken(targetBody, 0.46),
        Edge = SlugEdge.Top,
        Cw = rng.Next(2) == 0,
        OnEdge = true,
        LastDragAt = Stopwatch.GetTimestamp()
    };

    ResetMesh(blob, cx, cy, scale);
    return blob;
}

void ResetMesh(SlugBlob blob, double cx, double cy, double scale)
{
    var spX = BaseSpX * scale;
    var spY = BaseSpY * scale;
    var ox = cx - (MCols - 1) * spX / 2.0;
    var oy = cy - (MRows - 1) * spY / 2.0;

    for (int r = 0; r < MRows; r++)
    for (int c = 0; c < MCols; c++)
    {
        var i = r * MCols + c;
        blob.Px[i] = ox + c * spX;
        blob.Py[i] = oy + r * spY;
        blob.Vx[i] = 0;
        blob.Vy[i] = 0;
    }
}

bool TryFindNearestBlobPoint(double visX, double visY, out SlugBlob? hitBlob, out int hitPoint)
{
    hitBlob = null;
    hitPoint = -1;
    var best = GrabR2;

    foreach (var blob in blobs)
    {
        for (int i = 0; i < NP; i++)
        {
            var dx = visX - blob.Px[i];
            var dy = visY - blob.Py[i];
            var d2 = dx * dx + dy * dy;
            if (d2 >= best) continue;

            best = d2;
            hitBlob = blob;
            hitPoint = i;
        }
    }

    return hitBlob is not null;
}

void UpdateDrag(SlugBlob blob, double nextX, double nextY)
{
    var nowTicks = Stopwatch.GetTimestamp();
    var dt = Math.Max((nowTicks - blob.LastDragAt) / (double)Stopwatch.Frequency, 1e-3);
    var instVx = (nextX - blob.LastDragX) / dt;
    var instVy = (nextY - blob.LastDragY) / dt;

    var prevVx = blob.DragVelX;
    var prevVy = blob.DragVelY;
    var prevSpeed = Math.Sqrt(prevVx * prevVx + prevVy * prevVy);
    var instSpeed = Math.Sqrt(instVx * instVx + instVy * instVy);

    if (prevSpeed > 18 && instSpeed > 18)
    {
        var alignment = (prevVx * instVx + prevVy * instVy) / (prevSpeed * instSpeed);
        if (alignment < -0.35)
        {
            blob.ShakeEnergy += Math.Min(140.0, (prevSpeed + instSpeed) * 0.22);
        }
    }

    blob.ShakeEnergy = Math.Max(0.0, blob.ShakeEnergy - dt * 18.0);
    blob.DragVelX = prevVx * 0.35 + instVx * 0.65;
    blob.DragVelY = prevVy * 0.35 + instVy * 0.65;
    blob.LastDragX = nextX;
    blob.LastDragY = nextY;
    blob.LastDragAt = nowTicks;
    blob.GrabX = nextX;
    blob.GrabY = nextY;
}

void MaybeExplode(SlugBlob blob)
{
    var now = animationClock.Elapsed;
    if (blob.ShakeEnergy < SplitShakeThreshold ||
        now < blob.SplitCooldownUntil)
    {
        return;
    }

    var pieces = rng.Next(MinSplitPieces, MaxSplitPieces + 1);
    var (cx, cy) = GetCenter(blob);
    var (avgVx, avgVy) = GetAverageVelocity(blob);

    blob.TargetScale = 0.76;
    blob.TargetBody = PickSplitColor(blob.Id);
    blob.TargetOutline = Darken(blob.TargetBody, 0.46);
    blob.OnEdge = false;
    blob.FreeFlightUntil = now + TimeSpan.FromMilliseconds(500);
    blob.SplitCooldownUntil = now + TimeSpan.FromMilliseconds(1400);
    blob.ShakeEnergy = 0;

    if (blob.Grabbed < 0)
    {
        for (int i = 0; i < NP; i++)
        {
            blob.Vx[i] += avgVx * 0.2;
            blob.Vy[i] += avgVy * 0.2;
        }
    }

    var baseAngle = rng.NextDouble() * Math.PI * 2.0;
    for (int p = 1; p < pieces; p++)
    {
        var angle = baseAngle + (Math.PI * 2.0 * p / pieces) + (rng.NextDouble() - 0.5) * 0.25;
        var offsetX = Math.Cos(angle) * 2.4;
        var offsetY = Math.Sin(angle) * 2.1;
        var scale = 0.72 + rng.NextDouble() * 0.06;

        var child = CreateBlob(cx + offsetX, cy + offsetY, scale, blob.CurrentBody, PickSplitColor(blob.Id + p));
        child.OnEdge = false;
        child.Edge = blob.Edge;
        child.Cw = rng.Next(2) == 0;
        child.CrawlPhase = blob.CrawlPhase + p * 0.8;
        child.FreeFlightUntil = now + TimeSpan.FromMilliseconds(500);
        child.SplitCooldownUntil = now + TimeSpan.FromMilliseconds(1400);

        var burst = 14.0 + rng.NextDouble() * 7.0;
        for (int i = 0; i < NP; i++)
        {
            child.Vx[i] = avgVx + Math.Cos(angle) * burst;
            child.Vy[i] = avgVy + Math.Sin(angle) * burst;
        }

        blobs.Add(child);
    }
}

Rgb PickSplitColor(int seed)
{
    var palette = new[]
    {
        new Rgb(90, 220, 120),
        new Rgb(80, 205, 240),
        new Rgb(220, 185, 95),
        new Rgb(205, 120, 230),
        new Rgb(245, 125, 175)
    };

    return palette[Math.Abs(seed + rng.Next(palette.Length)) % palette.Length];
}

void ApplyFlickImpulse(SlugBlob blob)
{
    var impulseX = Math.Clamp(blob.DragVelX * FlickBoost, -MaxFlickSpeed, MaxFlickSpeed);
    var impulseY = Math.Clamp(blob.DragVelY * FlickBoost, -MaxFlickSpeed, MaxFlickSpeed);
    var throwSpeed = Math.Sqrt(impulseX * impulseX + impulseY * impulseY);

    blob.FreeFlightUntil = animationClock.Elapsed +
        TimeSpan.FromMilliseconds(180 + Math.Min(220, throwSpeed * 3));

    for (int i = 0; i < NP; i++)
    {
        blob.Vx[i] += impulseX;
        blob.Vy[i] += impulseY;
    }

    blob.DragVelX = 0;
    blob.DragVelY = 0;
}

void UpdateWorld(double dt, TimeSpan now)
{
    var hasMouse = TryGetMouseVisual(out var mouseX, out var mouseY);

    foreach (var blob in blobs)
    {
        blob.Scale = blob.Scale + (blob.TargetScale - blob.Scale) * (1 - Math.Exp(-3.2 * dt));
        blob.CurrentBody = Lerp(blob.CurrentBody, blob.TargetBody, 1 - Math.Exp(-1.4 * dt));
        blob.CurrentOutline = Lerp(blob.CurrentOutline, blob.TargetOutline, 1 - Math.Exp(-1.4 * dt));
        StepBlob(blob, dt);
        TransitionBlob(blob, now);
        UpdateGaze(blob, hasMouse, mouseX, mouseY, dt);
    }

    TryMergeBlobs(now);
}

void UpdateGaze(SlugBlob blob, bool hasMouse, double mouseX, double mouseY, double dt)
{
    var (cx, cy) = GetCenter(blob);
    var (vx, vy) = GetAverageVelocity(blob);
    var speed = Math.Sqrt(vx * vx + vy * vy);

    double desiredX;
    double desiredY;

    if (speed > 0.2)
    {
        desiredX = vx / speed;
        desiredY = vy / speed;
    }
    else
    {
        GetEdgeFrame(blob.Edge, blob.Cw, out desiredX, out desiredY, out _, out _);
        if (Math.Abs(desiredX) < 0.001 && Math.Abs(desiredY) < 0.001)
        {
            desiredX = blob.LookX == 0 && blob.LookY == 0 ? 1.0 : blob.LookX;
            desiredY = blob.LookY;
        }
    }

    if (hasMouse)
    {
        var dx = mouseX - cx;
        var dy = mouseY - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > 0.001)
        {
            dx /= dist;
            dy /= dist;
            var influence = Math.Clamp(1.0 - dist / (12.0 + blob.Scale * 5.0), 0.0, 1.0);
            desiredX = desiredX * (1.0 - influence) + dx * influence * 1.35;
            desiredY = desiredY * (1.0 - influence) + dy * influence * 1.35;
        }
    }

    var len = Math.Sqrt(desiredX * desiredX + desiredY * desiredY);
    if (len > 0.001)
    {
        desiredX /= len;
        desiredY /= len;
    }

    var ease = 1 - Math.Exp(-9.0 * dt);
    blob.LookX += (desiredX - blob.LookX) * ease;
    blob.LookY += (desiredY - blob.LookY) * ease;
}

bool TryGetMouseVisual(out double x, out double y)
{
    x = 0;
    y = 0;

    if (theApp is null || mouseXField is null || mouseYField is null)
    {
        return false;
    }

    if (mouseXField.GetValue(theApp) is not int mouseX ||
        mouseYField.GetValue(theApp) is not int mouseY ||
        mouseX < 0 ||
        mouseY < 0)
    {
        return false;
    }

    x = mouseX + 0.5;
    y = mouseY * 2.0 + 1.0;
    return true;
}

void StepBlob(SlugBlob blob, double dt)
{
    var wallR = scrW - 0.5;
    var wallB = scrH * 2.0 - 0.5;
    var crawling = blob.OnEdge && blob.Grabbed < 0;
    var reachEdge = blob.Edge;
    var reachAmount = 0.0;
    var reaching = blob.Grabbed >= 0 && TryGetReachEdge(blob, out reachEdge, out reachAmount);

    var gx = 0.0;
    var gy = Grav;
    if (blob.OnEdge)
    {
        (gx, gy) = blob.Edge switch
        {
            SlugEdge.Top => (0.0, -EdgeStick),
            SlugEdge.Bottom => (0.0, EdgeStick),
            SlugEdge.Left => (-EdgeStick, 0.0),
            SlugEdge.Right => (EdgeStick, 0.0),
            _ => (0.0, Grav)
        };
    }

    var tx = 0.0;
    var ty = 0.0;
    var nx = 0.0;
    var ny = 0.0;
    var reachTx = 0.0;
    var reachTy = 0.0;
    var reachNx = 0.0;
    var reachNy = 0.0;

    if (crawling)
    {
        blob.CrawlPhase += dt * CrawlCadence * Math.PI * 2.0;
        GetEdgeFrame(blob.Edge, blob.Cw, out tx, out ty, out nx, out ny);
    }
    else if (reaching)
    {
        blob.CrawlPhase += dt * CrawlCadence * Math.PI * 1.6;
        blob.Edge = reachEdge;
        GetEdgeFrame(reachEdge, blob.Cw, out reachTx, out reachTy, out reachNx, out reachNy);
    }

    for (int i = 0; i < NP; i++)
    {
        if (i == blob.Grabbed) continue;

        blob.Vx[i] += gx * dt;
        blob.Vy[i] += gy * dt;

        if (crawling)
        {
            var phase = blob.CrawlPhase - (1.0 - CrawlLead(blob.Edge, blob.Cw, i)) * CrawlLag;
            var stride = 0.16 + 0.84 * Math.Max(0.0, Math.Sin(phase));
            var tangentialSpeed = blob.Vx[i] * tx + blob.Vy[i] * ty;
            var drive = (CrawlF * stride - tangentialSpeed) * 4.0 * dt;

            blob.Vx[i] += tx * drive;
            blob.Vy[i] += ty * drive;

            var lift = Math.Sin(phase - 1.0);
            blob.Vx[i] += -nx * lift * CrawlLift * dt;
            blob.Vy[i] += -ny * lift * CrawlLift * dt;
        }
        else if (reaching)
        {
            var fromGrabX = blob.Px[i] - blob.GrabX;
            var fromGrabY = blob.Py[i] - blob.GrabY;
            var freedom = Math.Clamp(
                Math.Sqrt(fromGrabX * fromGrabX + fromGrabY * fromGrabY) / Math.Max(1.0, 2.4 * blob.Scale),
                0.0,
                1.0);
            var edgeLead = Math.Max(0.0, fromGrabX * reachNx + fromGrabY * reachNy);
            var stretch = (0.25 + 0.75 * freedom) *
                (0.7 + 0.3 * Math.Clamp(edgeLead / Math.Max(1.0, 1.6 * blob.Scale), 0.0, 1.0));

            var normalSpeed = blob.Vx[i] * reachNx + blob.Vy[i] * reachNy;
            var pull = (ReachPull * reachAmount * stretch - normalSpeed * 0.55) * dt;
            blob.Vx[i] += reachNx * pull;
            blob.Vy[i] += reachNy * pull;

            var phase = blob.CrawlPhase - (1.0 - CrawlLead(reachEdge, blob.Cw, i)) * (CrawlLag * 0.7);
            var sway = Math.Sin(phase);
            blob.Vx[i] += reachTx * sway * ReachSquirm * reachAmount * stretch * dt;
            blob.Vy[i] += reachTy * sway * ReachSquirm * reachAmount * stretch * dt;
        }
    }

    for (int s = 0; s < sprA.Length; s++)
    {
        var a = sprA[s];
        var b = sprB[s];
        var dx = blob.Px[b] - blob.Px[a];
        var dy = blob.Py[b] - blob.Py[a];
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001) continue;

        var relVx = blob.Vx[b] - blob.Vx[a];
        var relVy = blob.Vy[b] - blob.Vy[a];
        var relAlong = (relVx * dx + relVy * dy) / dist;
        var rest = sprR[s] * blob.Scale;
        var force = ((dist - rest) * Stiff + relAlong * SpringDamp) * dt / dist;
        var fx = dx * force;
        var fy = dy * force;

        if (a != blob.Grabbed) { blob.Vx[a] += fx; blob.Vy[a] += fy; }
        if (b != blob.Grabbed) { blob.Vx[b] -= fx; blob.Vy[b] -= fy; }
    }

    for (int i = 0; i < NP; i++)
    {
        if (i == blob.Grabbed)
        {
            blob.Px[i] = blob.GrabX;
            blob.Py[i] = blob.GrabY;
            blob.Vx[i] = 0;
            blob.Vy[i] = 0;
            continue;
        }

        var damping = crawling ? EdgeDamp : FreeDamp;
        blob.Vx[i] *= damping;
        blob.Vy[i] *= damping;
        blob.Px[i] += blob.Vx[i] * dt;
        blob.Py[i] += blob.Vy[i] * dt;

        if (blob.Px[i] < 0.5) { blob.Px[i] = 0.5; blob.Vx[i] *= -0.3; }
        if (blob.Px[i] > wallR) { blob.Px[i] = wallR; blob.Vx[i] *= -0.3; }
        if (blob.Py[i] < 0.5) { blob.Py[i] = 0.5; blob.Vy[i] *= -0.3; }
        if (blob.Py[i] > wallB) { blob.Py[i] = wallB; blob.Vy[i] *= -0.3; }
    }
}

bool TryGetReachEdge(SlugBlob blob, out SlugEdge edge, out double amount)
{
    var left = blob.GrabX;
    var right = scrW - blob.GrabX;
    var top = blob.GrabY;
    var bottom = scrH * 2.0 - blob.GrabY;

    edge = SlugEdge.Top;
    var nearest = top;

    if (right < nearest)
    {
        nearest = right;
        edge = SlugEdge.Right;
    }

    if (bottom < nearest)
    {
        nearest = bottom;
        edge = SlugEdge.Bottom;
    }

    if (left < nearest)
    {
        nearest = left;
        edge = SlugEdge.Left;
    }

    amount = Math.Clamp(1.0 - nearest / ReachRange, 0.0, 1.0);
    return amount > 0.0;
}

void GetEdgeFrame(SlugEdge edge, bool cw, out double tx, out double ty, out double nx, out double ny)
{
    tx = 0;
    ty = 0;
    nx = 0;
    ny = 0;

    switch (edge)
    {
        case SlugEdge.Top:
            tx = cw ? 1.0 : -1.0;
            ny = -1.0;
            break;
        case SlugEdge.Bottom:
            tx = cw ? -1.0 : 1.0;
            ny = 1.0;
            break;
        case SlugEdge.Left:
            ty = cw ? -1.0 : 1.0;
            nx = -1.0;
            break;
        case SlugEdge.Right:
            ty = cw ? 1.0 : -1.0;
            nx = 1.0;
            break;
    }
}

double CrawlLead(SlugEdge edge, bool cw, int index)
{
    var row = index / MCols;
    var col = index % MCols;

    return edge switch
    {
        SlugEdge.Top => cw ? col / (double)(MCols - 1) : (MCols - 1 - col) / (double)(MCols - 1),
        SlugEdge.Bottom => cw ? (MCols - 1 - col) / (double)(MCols - 1) : col / (double)(MCols - 1),
        SlugEdge.Left => cw ? (MRows - 1 - row) / (double)(MRows - 1) : row / (double)(MRows - 1),
        SlugEdge.Right => cw ? row / (double)(MRows - 1) : (MRows - 1 - row) / (double)(MRows - 1),
        _ => 0.0
    };
}

void TransitionBlob(SlugBlob blob, TimeSpan now)
{
    if (blob.Grabbed >= 0) return;

    var (comX, comY) = GetCenter(blob);
    var (avgVx, avgVy) = GetAverageVelocity(blob);

    var wR = (double)scrW;
    var wB = scrH * 2.0;
    const double snap = 3.0;
    const double settle = 8.0;
    var speed = Math.Sqrt(avgVx * avgVx + avgVy * avgVy);

    if (!blob.OnEdge)
    {
        if (now < blob.FreeFlightUntil) return;

        if (comY > wB - snap && Math.Abs(avgVy) < settle && speed < settle)
        { blob.OnEdge = true; blob.Edge = SlugEdge.Bottom; blob.Cw = rng.Next(2) == 0; }
        else if (comY < snap && Math.Abs(avgVy) < settle && speed < settle)
        { blob.OnEdge = true; blob.Edge = SlugEdge.Top; blob.Cw = rng.Next(2) == 0; }
        else if (comX < snap && Math.Abs(avgVx) < settle && speed < settle)
        { blob.OnEdge = true; blob.Edge = SlugEdge.Left; blob.Cw = rng.Next(2) == 0; }
        else if (comX > wR - snap && Math.Abs(avgVx) < settle && speed < settle)
        { blob.OnEdge = true; blob.Edge = SlugEdge.Right; blob.Cw = rng.Next(2) == 0; }
        return;
    }

    const double cm = 2.5;
    var corner = false;
    var next = blob.Edge;

    if (blob.Cw)
    {
        switch (blob.Edge)
        {
            case SlugEdge.Top when comX > wR - cm: next = SlugEdge.Right; corner = true; break;
            case SlugEdge.Right when comY > wB - cm: next = SlugEdge.Bottom; corner = true; break;
            case SlugEdge.Bottom when comX < cm: next = SlugEdge.Left; corner = true; break;
            case SlugEdge.Left when comY < cm: next = SlugEdge.Top; corner = true; break;
        }
    }
    else
    {
        switch (blob.Edge)
        {
            case SlugEdge.Top when comX < cm: next = SlugEdge.Left; corner = true; break;
            case SlugEdge.Left when comY > wB - cm: next = SlugEdge.Bottom; corner = true; break;
            case SlugEdge.Bottom when comX > wR - cm: next = SlugEdge.Right; corner = true; break;
            case SlugEdge.Right when comY < cm: next = SlugEdge.Top; corner = true; break;
        }
    }

    if (corner)
    {
        blob.Edge = next;
        if (rng.Next(2) == 0) blob.Cw = !blob.Cw;
    }
}

void TryMergeBlobs(TimeSpan now)
{
    for (int i = 0; i < blobs.Count; i++)
    for (int j = i + 1; j < blobs.Count; j++)
    {
        var a = blobs[i];
        var b = blobs[j];

        if (now < a.FreeFlightUntil || now < b.FreeFlightUntil) continue;

        var (ax, ay) = GetCenter(a);
        var (bx, by) = GetCenter(b);
        var dx = bx - ax;
        var dy = by - ay;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > MergeDistance * (a.Scale + b.Scale) * 0.5) continue;

        var combining = a.Grabbed >= 0 || b.Grabbed >= 0;
        if (!combining)
        {
            var (avx, avy) = GetAverageVelocity(a);
            var (bvx, bvy) = GetAverageVelocity(b);
            var rel = Math.Sqrt(Sq(avx - bvx) + Sq(avy - bvy));
            if (rel > 5.0) continue;
        }

        MergeBlobs(a, b, now);
        blobs.RemoveAll(blob => blob.Removed);
        return;
    }
}

void MergeBlobs(SlugBlob a, SlugBlob b, TimeSpan now)
{
    var primary = a.Grabbed >= 0 ? a : b.Grabbed >= 0 ? b : a;
    var secondary = ReferenceEquals(primary, a) ? b : a;

    var (pcx, pcy) = GetCenter(primary);
    var (scx, scy) = GetCenter(secondary);
    var (svx, svy) = GetAverageVelocity(secondary);

    var shiftX = (scx - pcx) * (primary.Grabbed >= 0 ? 0.10 : 0.35);
    var shiftY = (scy - pcy) * (primary.Grabbed >= 0 ? 0.10 : 0.35);
    MoveBlob(primary, shiftX, shiftY);

    primary.TargetScale = Math.Min(1.0, primary.TargetScale + secondary.TargetScale * 0.55);
    primary.TargetBody = Lerp(primary.TargetBody, secondary.TargetBody, 0.5);
    primary.TargetOutline = Darken(primary.TargetBody, 0.46);
    primary.FreeFlightUntil = now + TimeSpan.FromMilliseconds(160);
    primary.SplitCooldownUntil = now + TimeSpan.FromMilliseconds(900);

    for (int i = 0; i < NP; i++)
    {
        primary.Vx[i] = primary.Vx[i] * 0.72 + svx * 0.28;
        primary.Vy[i] = primary.Vy[i] * 0.72 + svy * 0.28;
    }

    secondary.Removed = true;
}

void MoveBlob(SlugBlob blob, double dx, double dy)
{
    for (int i = 0; i < NP; i++)
    {
        blob.Px[i] += dx;
        blob.Py[i] += dy;
    }

    if (blob.Grabbed >= 0)
    {
        blob.GrabX += dx;
        blob.GrabY += dy;
    }
}

(double X, double Y) GetCenter(SlugBlob blob)
{
    var x = 0.0;
    var y = 0.0;

    for (int i = 0; i < NP; i++)
    {
        x += blob.Px[i];
        y += blob.Py[i];
    }

    return (x / NP, y / NP);
}

(double X, double Y) GetAverageVelocity(SlugBlob blob)
{
    var x = 0.0;
    var y = 0.0;

    for (int i = 0; i < NP; i++)
    {
        x += blob.Vx[i];
        y += blob.Vy[i];
    }

    return (x / NP, y / NP);
}

void RenderSlugs(Surface surface)
{
    List<BlobSnapshot> snapshots;
    lock (lk)
    {
        snapshots = new List<BlobSnapshot>(blobs.Count);
        foreach (var blob in blobs.OrderBy(b => b.Grabbed >= 0 ? 1 : 0))
        {
            var px = new double[NP];
            var py = new double[NP];
            Array.Copy(blob.Px, px, NP);
            Array.Copy(blob.Py, py, NP);

            snapshots.Add(new BlobSnapshot(
                px,
                py,
                blob.Scale,
                blob.LookX,
                blob.LookY,
                ToHex(blob.CurrentBody),
                ToHex(blob.CurrentOutline)));
        }
    }

    foreach (var blob in snapshots)
    {
        DrawBlob(surface, blob);
    }
}

void DrawBlob(Surface surface, BlobSnapshot blob)
{
    var sx = blob.Px;
    var sy = blob.Py;
    var eX = sx[EyeIdx];
    var eY = sy[EyeIdx];
    var solidCells = new List<(int X, int Y)>();

    var mnX = double.MaxValue;
    var mxX = double.MinValue;
    var mnY = double.MaxValue;
    var mxY = double.MinValue;

    for (int i = 0; i < NP; i++)
    {
        if (sx[i] < mnX) mnX = sx[i];
        if (sx[i] > mxX) mxX = sx[i];
        if (sy[i] < mnY) mnY = sy[i];
        if (sy[i] > mxY) mxY = sy[i];
    }

    var radius = BlobR * blob.Scale;
    var radiusSq = radius * radius;
    var c0 = Math.Max(0, (int)(mnX - radius - 1));
    var c1 = Math.Min(surface.Width - 1, (int)(mxX + radius + 2));
    var r0 = Math.Max(0, (int)((mnY - radius - 1) / 2.0));
    var r1 = Math.Min(surface.Height - 1, (int)((mxY + radius + 2) / 2.0));

    for (int row = r0; row <= r1; row++)
    for (int col = c0; col <= c1; col++)
    {
        var hx = col + 0.5;
        var tvy = row * 2.0 + 0.5;
        var bvy = row * 2.0 + 1.5;

        var tF = 0.0;
        var bF = 0.0;
        for (int i = 0; i < NP; i++)
        {
            var dx = hx - sx[i];
            var dyt = tvy - sy[i];
            var dyb = bvy - sy[i];
            tF += radiusSq / Math.Max(0.01, dx * dx + dyt * dyt);
            bF += radiusSq / Math.Max(0.01, dx * dx + dyb * dyb);
        }

        var tHit = tF >= Thresh;
        var bHit = bF >= Thresh;
        if (!tHit && !bHit) continue;

        Hex1bColor Pick(double field)
            => field > Thresh + 0.4 ? blob.Body : blob.Outline;

        string ch;
        Hex1bColor fg;
        Hex1bColor? bg;

        if (tHit && bHit)
        {
            var tc = Pick(tF);
            var bc = Pick(bF);
            if (tc.R == bc.R && tc.G == bc.G && tc.B == bc.B)
            {
                ch = "\u2588";
                fg = tc;
                bg = null;
                solidCells.Add((col, row));
            }
            else
            {
                ch = "\u2580";
                fg = tc;
                bg = bc;
            }
        }
        else if (tHit)
        {
            ch = "\u2580";
            fg = Pick(tF);
            bg = null;
        }
        else
        {
            ch = "\u2584";
            fg = Pick(bF);
            bg = null;
        }

        surface[col, row] = new SurfaceCell(ch, fg, bg, CellAttributes.None, 1);
    }

    DrawBrailleEyes(surface, blob, solidCells, eX, eY);
}

void DrawBrailleEyes(
    Surface surface,
    BlobSnapshot blob,
    List<(int X, int Y)> solidCells,
    double eyeX,
    double eyeY)
{
    if (solidCells.Count == 0)
    {
        return;
    }

    var rowAnchor = eyeY / 2.0 - 0.15 * blob.Scale;
    var leftAnchorX = eyeX - 0.55 * blob.Scale;
    var rightAnchorX = eyeX + 0.55 * blob.Scale;
    var used = new HashSet<(int X, int Y)>();

    PlaceEye(leftAnchorX, rowAnchor);
    PlaceEye(rightAnchorX, rowAnchor);

    void PlaceEye(double anchorX, double anchorY)
    {
        (int X, int Y)? best = null;
        var bestDistance = double.MaxValue;

        foreach (var cell in solidCells)
        {
            if (used.Contains(cell)) continue;

            var dx = cell.X + 0.5 - anchorX;
            var dy = cell.Y + 0.5 - anchorY;
            var distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = cell;
        }

        if (best is not { } eyeCell)
        {
            return;
        }

        used.Add(eyeCell);
        var under = surface[eyeCell.X, eyeCell.Y];
        var bgColor = under.Foreground ?? blob.Body;
        surface[eyeCell.X, eyeCell.Y] = new SurfaceCell(
            GetBrailleLookGlyph(blob.LookX, blob.LookY),
            eyePupil,
            bgColor,
            CellAttributes.None,
            1);
    }
}

string GetBrailleLookGlyph(double lookX, double lookY)
{
    var horizontal = lookX < -0.33 ? -1 : lookX > 0.33 ? 1 : 0;
    var vertical = lookY < -0.33 ? -1 : lookY > 0.33 ? 1 : 0;

    int leftMask;
    int rightMask;

    switch (vertical)
    {
        case < 0:
            leftMask = 0b00000011;
            rightMask = 0b00011000;
            break;
        case > 0:
            leftMask = 0b00000110;
            rightMask = 0b00110000;
            break;
        default:
            leftMask = 0b00000010;
            rightMask = 0b00010000;
            break;
    }

    var mask = horizontal switch
    {
        < 0 => leftMask,
        > 0 => rightMask,
        _ => leftMask | rightMask
    };

    return char.ConvertFromUtf32(0x2800 + mask);
}

Rgb Lerp(Rgb a, Rgb b, double t)
    => new(
        a.R + (b.R - a.R) * t,
        a.G + (b.G - a.G) * t,
        a.B + (b.B - a.B) * t);

Rgb Darken(Rgb color, double factor)
    => new(color.R * factor, color.G * factor, color.B * factor);

Hex1bColor ToHex(Rgb color)
    => Hex1bColor.FromRgb(
        (byte)Math.Clamp((int)Math.Round(color.R), 0, 255),
        (byte)Math.Clamp((int)Math.Round(color.G), 0, 255),
        (byte)Math.Clamp((int)Math.Round(color.B), 0, 255));

double Sq(double v) => v * v;

enum SlugEdge { Top, Right, Bottom, Left }

sealed class SlugBlob
{
    public required int Id { get; init; }
    public double[] Px { get; } = new double[12];
    public double[] Py { get; } = new double[12];
    public double[] Vx { get; } = new double[12];
    public double[] Vy { get; } = new double[12];
    public int Grabbed { get; set; } = -1;
    public double GrabX { get; set; }
    public double GrabY { get; set; }
    public bool OnEdge { get; set; }
    public SlugEdge Edge { get; set; }
    public bool Cw { get; set; }
    public double DragVelX { get; set; }
    public double DragVelY { get; set; }
    public double LastDragX { get; set; }
    public double LastDragY { get; set; }
    public long LastDragAt { get; set; }
    public double ShakeEnergy { get; set; }
    public TimeSpan FreeFlightUntil { get; set; }
    public TimeSpan SplitCooldownUntil { get; set; }
    public double CrawlPhase { get; set; }
    public double Scale { get; set; } = 1.0;
    public double TargetScale { get; set; } = 1.0;
    public Rgb CurrentBody { get; set; }
    public Rgb TargetBody { get; set; }
    public Rgb CurrentOutline { get; set; }
    public Rgb TargetOutline { get; set; }
    public double LookX { get; set; } = 1.0;
    public double LookY { get; set; }
    public bool Removed { get; set; }
}

readonly record struct BlobSnapshot(
    double[] Px,
    double[] Py,
    double Scale,
    double LookX,
    double LookY,
    Hex1bColor Body,
    Hex1bColor Outline);

readonly record struct Rgb(double R, double G, double B);
