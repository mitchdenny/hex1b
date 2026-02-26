using System.Diagnostics;
using System.Numerics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using ModelViewerDemo;

// Build the model catalog: built-in models + optional file arg
var models = new List<(string Name, Func<Mesh> Load)>();

foreach (var name in MeshGenerator.ModelNames)
    models.Add((name, () => MeshGenerator.Create(name)));

// Parse command-line args for additional file-based models
for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--file" or "-f" && i + 1 < args.Length)
    {
        var path = args[++i];
        var fileName = Path.GetFileNameWithoutExtension(path);
        models.Add((fileName, () => ObjParser.ParseFile(path).Normalize()));
    }
}

// State
int modelIndex = 0;
int lodIndex = 0;
Mesh originalMesh = models[modelIndex].Load();
(string Label, Mesh Mesh)[] lodChain = MeshDecimator.BuildLodChain(originalMesh);
Mesh currentMesh = lodChain[lodIndex].Mesh;
string currentInfo = FormatInfo(models[modelIndex].Name, originalMesh, lodChain[lodIndex]);
var sw = Stopwatch.StartNew();

void SwitchModel(int newIndex)
{
    modelIndex = newIndex;
    lodIndex = 0;
    originalMesh = models[modelIndex].Load();
    lodChain = MeshDecimator.BuildLodChain(originalMesh);
    currentMesh = lodChain[lodIndex].Mesh;
    currentInfo = FormatInfo(models[modelIndex].Name, originalMesh, lodChain[lodIndex]);
}

void SwitchLod(int newLod)
{
    lodIndex = Math.Clamp(newLod, 0, lodChain.Length - 1);
    currentMesh = lodChain[lodIndex].Mesh;
    currentInfo = FormatInfo(models[modelIndex].Name, originalMesh, lodChain[lodIndex]);
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.Interactable(ic =>
            ic.Surface(s =>
            {
                float elapsed = (float)sw.Elapsed.TotalSeconds;
                float angleY = elapsed * 0.8f;
                float angleX = 0.3f;

                var rotation = Quaternion.CreateFromYawPitchRoll(angleY, angleX, 0);

                var buffer = new BrailleBuffer(s.Width, s.Height);
                Renderer.Render(currentMesh, buffer, rotation);

                return
                [
                    s.Layer(surface =>
                    {
                        surface.Clear();
                        var fg = Hex1bColor.FromRgb(0, 200, 120);
                        var dimFg = Hex1bColor.FromRgb(100, 100, 100);
                        var hintFg = Hex1bColor.FromRgb(70, 70, 70);

                        for (int cy = 0; cy < s.Height; cy++)
                        {
                            for (int cx = 0; cx < s.Width; cx++)
                            {
                                var ch = buffer.GetChar(cx, cy);
                                if (ch.HasValue)
                                {
                                    surface.WriteChar(cx, cy, ch.Value,
                                        foreground: fg);
                                }
                            }
                        }

                        // Info bar at bottom
                        surface.WriteText(0, s.Height - 1, currentInfo,
                            foreground: dimFg);

                        // Navigation hint
                        var hint = $"◄► model ({modelIndex + 1}/{models.Count})  ▲▼ LOD ({lodChain[lodIndex].Label})";
                        int hintX = Math.Max(0, s.Width - hint.Length);
                        surface.WriteText(hintX, s.Height - 1, hint,
                            foreground: hintFg);
                    })
                ];
            })
            .RedrawAfter(33)
        )
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.RightArrow).Action(_ =>
            {
                SwitchModel((modelIndex + 1) % models.Count);
                app.Invalidate();
            }, "Next model");

            bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
            {
                SwitchModel((modelIndex - 1 + models.Count) % models.Count);
                app.Invalidate();
            }, "Previous model");

            bindings.Key(Hex1bKey.DownArrow).Action(_ =>
            {
                if (lodIndex < lodChain.Length - 1)
                {
                    SwitchLod(lodIndex + 1);
                    app.Invalidate();
                }
            }, "Reduce polygons");

            bindings.Key(Hex1bKey.UpArrow).Action(_ =>
            {
                if (lodIndex > 0)
                {
                    SwitchLod(lodIndex - 1);
                    app.Invalidate();
                }
            }, "Increase polygons");
        })
    )
    .Build();

await terminal.RunAsync();

static string FormatInfo(string name, Mesh original, (string Label, Mesh Mesh) lod)
{
    var m = lod.Mesh;
    string reduction = lod.Label == "100%"
        ? ""
        : $" (LOD {lod.Label} of {original.Faces.Length})";
    return $" {name} | {m.Vertices.Length} verts | {m.Faces.Length} faces | {m.Edges.Length} edges{reduction} ";
}
