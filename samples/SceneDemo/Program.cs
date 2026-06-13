using System.Diagnostics;
using System.Linq;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Rendering;
using Hex1b.Scene.Textures;
using Hex1b.Theming;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

var scene = new SceneClass("Scene Demo");

// Create inner scene for texture rendering
var innerScene = CreateInnerScene();
var (innerCube, innerTorus, innerCylinder) = GetInnerSceneMeshes(innerScene);
var innerPerspectiveCamera = new ScenePerspectiveCamera("Inner Perspective");
var innerSceneWidget = new SceneWidget(innerScene, innerPerspectiveCamera);

var standardRenderables = CreateRenderables();
var planeRenderable = standardRenderables[^1]; // Last renderable is the plane
var planeMesh = (SceneMesh)planeRenderable.Mesh;
var planeTextureMaterial = (SceneTextureMaterial)planeMesh.Material!;

// Only add the plane to the main scene (not the primitive shapes - they're in inner scene)
scene.AddChild(planeMesh);
// Also add the wave cloth for comparison (renderables[3])
if (standardRenderables.Length > 3)
{
    scene.AddChild(standardRenderables[3].Mesh);
}
var metaball = CreateMetaballState();
var contentMode = SceneContentMode.Primitives;

var ambientLight = new SceneAmbientLight("Ambient");
var keyLight = new SceneDirectionalLight("Key");
var fillLight = new SceneDirectionalLight("Fill");
scene.AddChild(ambientLight);
scene.AddChild(keyLight);
scene.AddChild(fillLight);

var cameras = new (string Label, SceneCamera Camera)[]
{
    ("Perspective", new ScenePerspectiveCamera("Perspective")),
    ("Orthographic", new SceneOrthographicCamera("Orthographic")),
    ("Isometric", new SceneIsometricCamera("Isometric"))
};

var cameraTypeIndex = 0;
var renderMode = SceneRenderMode.Wireframe;
var cinematicLighting = true;
var orbitYaw = 0.0f;
var orbitRadius = 11.0f;
var orbitHeight = 1.0f;
var orbitYawVelocity = 0.0f;
var orbitZoomVelocity = 0.0f;
var polygonDetailLevel = PolygonDetailLevel.High;

// Plane rotation animation variables
var planeRoll = 0.0f;
var planePitch = 0.0f;
var planeYaw = 0.0f;

ApplyLightingSetup(cinematicLighting, ambientLight, keyLight, fillLight, standardRenderables, metaball);
ApplyMaterialMode(renderMode, standardRenderables, metaball);
ApplyPolygonDetailLevel(polygonDetailLevel, standardRenderables, metaball);
ApplySceneContent(scene, standardRenderables, metaball, contentMode);

var stopwatch = Stopwatch.StartNew();
var lastFrameSeconds = 0.0f;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(
        o => o.EnableMouse = true,
        app => ctx =>
        ctx.Interactable(ic =>
        {
            var frameSeconds = (float)stopwatch.Elapsed.TotalSeconds;
            var deltaSeconds = frameSeconds - lastFrameSeconds;
            if (deltaSeconds <= 0.0f)
            {
                deltaSeconds = 1.0f / 60.0f;
            }
            lastFrameSeconds = frameSeconds;

            orbitYaw += orbitYawVelocity * deltaSeconds;
            orbitRadius += orbitZoomVelocity * deltaSeconds;
            orbitRadius = Math.Clamp(orbitRadius, 4.0f, 22.0f);

            var damping = MathF.Exp(-4.2f * deltaSeconds);
            orbitYawVelocity *= damping;
            orbitZoomVelocity *= damping;

            if (MathF.Abs(orbitYawVelocity) < 0.001f)
                orbitYawVelocity = 0.0f;
            if (MathF.Abs(orbitZoomVelocity) < 0.001f)
                orbitZoomVelocity = 0.0f;

            for (var i = 0; i < standardRenderables.Length; i++)
            {
                var item = standardRenderables[i];
                var speed = item.RotationSpeed;
                item.Mesh.Rotation = Quaternion.FromEulerAngles(
                    frameSeconds * speed.X,
                    frameSeconds * speed.Y,
                    frameSeconds * speed.Z);
                item.Update?.Invoke(frameSeconds);
            }

            // Update inner scene objects rotation
            var innerSpeed = 0.8f;
            innerCube.Rotation = Quaternion.FromEulerAngles(frameSeconds * innerSpeed * 1.2f, frameSeconds * innerSpeed * 0.8f, frameSeconds * innerSpeed * 0.6f);
            innerTorus.Rotation = Quaternion.FromEulerAngles(frameSeconds * innerSpeed * 0.9f, frameSeconds * innerSpeed * 1.3f, frameSeconds * innerSpeed * 0.7f);
            innerCylinder.Rotation = Quaternion.FromEulerAngles(frameSeconds * innerSpeed * 0.7f, frameSeconds * innerSpeed * 0.5f, frameSeconds * innerSpeed * 1.4f);

            // Render inner scene widget to texture and update plane
            var dynamicTexture = RenderSceneWidgetToTexture(innerSceneWidget, 128, 128);
            planeTextureMaterial.Texture = dynamicTexture;

            // Update plane rotation on all 3 axes
            planeRoll += 0.4f * deltaSeconds;
            planePitch += 0.6f * deltaSeconds;
            planeYaw += 0.35f * deltaSeconds;
            planeMesh.Rotation = Quaternion.FromEulerAngles(planeRoll, planePitch, planeYaw);

            if (contentMode == SceneContentMode.Metaball)
            {
                UpdateMetaball(metaball, frameSeconds);
            }

            UpdateCameraTransforms(cameras, orbitYaw, orbitRadius, orbitHeight);

            var modeLabel = renderMode switch
            {
                SceneRenderMode.Wireframe => "Wireframe (Braille)",
                SceneRenderMode.NormalDebug => "Normal Debug",
                SceneRenderMode.DepthDebug => "Depth Debug",
                _ => "Solid Lit (Half-block)"
            };
            var lightLabel = cinematicLighting ? "Cinematic" : "Flat";
            var sceneLabel = contentMode switch
            {
                SceneContentMode.Metaball => "Metaball",
                SceneContentMode.WaveCloth => "Wave Cloth",
                _ => "Primitives"
            };
            var sceneTitle = contentMode switch
            {
                SceneContentMode.Metaball => "Morphing Metaball",
                SceneContentMode.WaveCloth => "Fabric Wave Plane",
                _ => "Torus • Cube • Cylinder"
            };
            var polygonLabel = polygonDetailLevel.ToString();
            var activeCamera = cameras[cameraTypeIndex];

            return ic.Grid(g =>
            {
                g.Columns.Add(SizeHint.Fill);
                g.Columns.Add(SizeHint.Fill);
                g.Rows.Add(SizeHint.Fixed(1));
                g.Rows.Add(SizeHint.Fill);
                g.Rows.Add(SizeHint.Fill);
                g.Rows.Add(SizeHint.Fixed(1));

                return
                [
                    g.Cell(c => c.Text(
                        $" Scene: {sceneLabel} | Mode: {modeLabel} | Camera: {activeCamera.Label} | Radius: {orbitRadius:0.0} | Light: {lightLabel} | Polys: {polygonLabel} "))
                        .Row(0).Column(0),

                    g.Cell(c => c.Border(b => [b.Scene(scene, activeCamera.Camera)]).Title(sceneTitle))
                        .Row(1).Column(0),

                    g.Cell(c => c.Border(b => [b.Scene(scene, activeCamera.Camera)]).Title(sceneTitle))
                        .Row(1).Column(1),

                    g.Cell(c => c.Border(b => [b.Scene(scene, activeCamera.Camera)]).Title(sceneTitle))
                        .Row(2).Column(0),

                    g.Cell(c => c.Border(b => [b.Scene(scene, activeCamera.Camera)]).Title(sceneTitle))
                        .Row(2).Column(1),

                    g.Cell(c => c.Text(
                        " ←/→ orbit   ↑/↓ zoom   drag L/R orbit   wheel/pinch zoom   C camera   W render mode   L light   M scene   P poly detail "))
                        .Row(3).Column(0)
                ];
            })
            .RedrawAfter(33);
        })
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
            {
                orbitYawVelocity -= 1.8f;
                app.Invalidate();
            }, "Orbit left");

            bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
            {
                orbitYawVelocity += 1.8f;
                app.Invalidate();
            }, "Orbit right");

            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ =>
            {
                orbitZoomVelocity -= 6.0f;
                app.Invalidate();
            }, "Zoom in");

            bindings.Key(Hex1bKey.DownArrow).Global().Action(_ =>
            {
                orbitZoomVelocity += 6.0f;
                app.Invalidate();
            }, "Zoom out");

            bindings.Drag(MouseButton.Left).Action((_, _) =>
            {
                var lastDx = 0;
                return new DragHandler(
                    (ctx, dx, _) =>
                    {
                        var stepDx = dx - lastDx;
                        lastDx = dx;
                        orbitYawVelocity += stepDx * 0.09f;
                        app.Invalidate();
                    });
            }, "Orbit by dragging");

            bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
            {
                orbitZoomVelocity -= 8.0f;
                app.Invalidate();
            }, "Zoom in with wheel/pinch");

            bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
            {
                orbitZoomVelocity += 8.0f;
                app.Invalidate();
            }, "Zoom out with wheel/pinch");

            bindings.Key(Hex1bKey.C).Global().Action(_ =>
            {
                cameraTypeIndex = (cameraTypeIndex + 1) % cameras.Length;
                app.Invalidate();
            }, "Toggle camera type");

            bindings.Key(Hex1bKey.W).Global().Action(_ =>
            {
                renderMode = renderMode switch
                {
                    SceneRenderMode.Wireframe => SceneRenderMode.Lit,
                    SceneRenderMode.Lit => SceneRenderMode.NormalDebug,
                    SceneRenderMode.NormalDebug => SceneRenderMode.DepthDebug,
                    _ => SceneRenderMode.Wireframe
                };
                ApplyMaterialMode(renderMode, standardRenderables, metaball);
                app.Invalidate();
            }, "Cycle render mode");

            bindings.Key(Hex1bKey.L).Global().Action(_ =>
            {
                cinematicLighting = !cinematicLighting;
                ApplyLightingSetup(cinematicLighting, ambientLight, keyLight, fillLight, standardRenderables, metaball);
                app.Invalidate();
            }, "Toggle light setup");

            bindings.Key(Hex1bKey.M).Global().Action(_ =>
            {
                contentMode = contentMode switch
                {
                    SceneContentMode.Primitives => SceneContentMode.Metaball,
                    SceneContentMode.Metaball => SceneContentMode.WaveCloth,
                    _ => SceneContentMode.Primitives
                };

                if (contentMode != SceneContentMode.Primitives)
                {
                    // Start single-model modes in solid/normal shading by default.
                    if (renderMode == SceneRenderMode.Wireframe)
                    {
                        renderMode = SceneRenderMode.Lit;
                    }
                }

                ApplyMaterialMode(renderMode, standardRenderables, metaball);
                ApplySceneContent(scene, standardRenderables, metaball, contentMode);
                app.Invalidate();
            }, "Cycle scene content");

            bindings.Key(Hex1bKey.P).Global().Action(_ =>
            {
                polygonDetailLevel = polygonDetailLevel switch
                {
                    PolygonDetailLevel.High => PolygonDetailLevel.Medium,
                    PolygonDetailLevel.Medium => PolygonDetailLevel.Low,
                    _ => PolygonDetailLevel.High
                };
                ApplyPolygonDetailLevel(polygonDetailLevel, standardRenderables, metaball);
                app.Invalidate();
            }, "Cycle polygon detail");
        })
    )
    .WithMouse()
    .Build();

await terminal.RunAsync();

static SceneRenderable[] CreateRenderables()
{
    var torusWire = new SceneLineBasicMaterial(new Vector3(1.00f, 0.55f, 0.25f));
    var torusSolid = new SceneMeshMaterial(new Vector3(0.90f, 0.45f, 0.20f));
    var torusGeometry = new PolygonDetailProfile<SceneBufferGeometry>(
        High: CreateTorusGeometry(0.95f, 0.32f, 28, 16),
        Medium: CreateTorusGeometry(0.95f, 0.32f, 20, 12),
        Low: CreateTorusGeometry(0.95f, 0.32f, 12, 8));
    var torus = new SceneMesh(torusGeometry.High, torusWire, "Torus")
    {
        Position = new Vector3(-2.9f, 0.0f, 0.0f)
    };

    var cubeWire = new SceneLineBasicMaterial(new Vector3(0.25f, 0.85f, 1.00f));
    var cubeSolid = new SceneMeshMaterial(new Vector3(0.20f, 0.75f, 0.95f));
    var cubeGeometry = CreateCubeGeometry();
    var cubeLod = new PolygonDetailProfile<SceneBufferGeometry>(
        High: cubeGeometry,
        Medium: cubeGeometry,
        Low: cubeGeometry);
    var cube = new SceneMesh(cubeGeometry, cubeWire, "Cube")
    {
        Position = new Vector3(0.0f, 0.0f, -2.9f)
    };

    var cylinderWire = new SceneLineBasicMaterial(new Vector3(0.45f, 1.00f, 0.50f));
    var cylinderSolid = new SceneMeshMaterial(new Vector3(0.35f, 0.90f, 0.40f));
    var cylinderGeometry = new PolygonDetailProfile<SceneBufferGeometry>(
        High: CreateCylinderGeometry(0.85f, 2.0f, 24),
        Medium: CreateCylinderGeometry(0.85f, 2.0f, 16),
        Low: CreateCylinderGeometry(0.85f, 2.0f, 10));
    var cylinder = new SceneMesh(cylinderGeometry.High, cylinderWire, "Cylinder")
    {
        Position = new Vector3(2.9f, 0.0f, 0.0f)
    };

    var waveWire = new SceneLineBasicMaterial(new Vector3(0.95f, 0.95f, 0.55f));
    var waveSolid = new SceneMeshMaterial(new Vector3(0.90f, 0.88f, 0.50f));
    var waveDetails = new PolygonDetailProfile<FabricWaveDetail>(
        High: CreateFabricWaveDetail(width: 2.8f, depth: 2.8f, segmentsX: 30, segmentsZ: 30),
        Medium: CreateFabricWaveDetail(width: 2.8f, depth: 2.8f, segmentsX: 20, segmentsZ: 20),
        Low: CreateFabricWaveDetail(width: 2.8f, depth: 2.8f, segmentsX: 12, segmentsZ: 12));

    var activeWaveDetail = waveDetails.High;
    var wave = new SceneMesh(activeWaveDetail.Geometry, waveWire, "Wave Cloth")
    {
        Position = new Vector3(0.0f, 0.0f, 2.9f),
        Rotation = Quaternion.FromEulerAngles(-0.35f, 0.0f, 0.0f)
    };

    // Create a textured plane for testing
    var planeGeometry = CreatePlaneGeometry(3.0f, 3.0f);
    var testTexture = CreateTestTexture(128, 128);
    var textureMaterial = new SceneTextureMaterial(new Vector3(1.0f, 1.0f, 1.0f), testTexture);
    var planeWire = new SceneLineBasicMaterial(new Vector3(1.0f, 1.0f, 1.0f));
    var plane = new SceneMesh(planeGeometry, planeWire, "Textured Plane")
    {
        Position = new Vector3(0.0f, -2.5f, 0.0f),
        Material = textureMaterial
    };
    
    return
    [
        new SceneRenderable(
            torus,
            torusWire,
            torusSolid,
            new Vector3(0.7f, 1.3f, 0.9f),
            torusGeometry),
        new SceneRenderable(
            cube,
            cubeWire,
            cubeSolid,
            new Vector3(1.2f, 0.9f, 0.6f),
            cubeLod),
        new SceneRenderable(
            cylinder,
            cylinderWire,
            cylinderSolid,
            new Vector3(0.9f, 1.1f, 1.4f),
            cylinderGeometry),
        new SceneRenderable(
            wave,
            waveWire,
            waveSolid,
            new Vector3(0.0f, 0.25f, 0.0f),
            new PolygonDetailProfile<SceneBufferGeometry>(
                High: waveDetails.High.Geometry,
                Medium: waveDetails.Medium.Geometry,
                Low: waveDetails.Low.Geometry),
            detailLevel => activeWaveDetail = waveDetails.Get(detailLevel),
            timeSeconds => UpdateFabricWave(activeWaveDetail, timeSeconds)),
        new SceneRenderable(
            plane,
            planeWire,
            textureMaterial,
            new Vector3(0.0f, 0.0f, 0.0f),
            new PolygonDetailProfile<SceneBufferGeometry>(
                High: planeGeometry,
                Medium: planeGeometry,
                Low: planeGeometry))
    ];
}

static SceneClass CreateInnerScene()
{
    var innerScene = new SceneClass("Inner Scene");
    
    // Create simple geometries with normal visualization
    var cubeGeom = CreateCubeGeometry();
    var torusGeom = CreateTorusGeometry(0.95f, 0.32f, 16, 10);
    var cylinderGeom = CreateCylinderGeometry(0.85f, 2.0f, 16);
    
    // All use same material for simplicity (normal debug material will be applied later)
    var material = new SceneMeshMaterial(Vector3.One);
    var linemat = new SceneLineBasicMaterial(Vector3.One);
    
    // Create meshes positioned in a triangle
    var cube = new SceneMesh(cubeGeom, linemat, "Inner Cube")
    {
        Position = new Vector3(-1.5f, 0.0f, 0.0f),
        Material = material
    };
    
    var torus = new SceneMesh(torusGeom, linemat, "Inner Torus")
    {
        Position = new Vector3(0.0f, 0.0f, -1.5f),
        Material = material
    };
    
    var cylinder = new SceneMesh(cylinderGeom, linemat, "Inner Cylinder")
    {
        Position = new Vector3(1.5f, 0.0f, 0.0f),
        Material = material
    };
    
    innerScene.AddChild(cube);
    innerScene.AddChild(torus);
    innerScene.AddChild(cylinder);
    
    // Add basic lighting
    var ambientLight = new SceneAmbientLight("Ambient") { Intensity = 0.4f };
    var directionalLight = new SceneDirectionalLight("Key")
    {
        Rotation = Quaternion.FromEulerAngles(-0.65f, 0.80f, 0.0f),
        Intensity = 1.0f
    };
    
    innerScene.AddChild(ambientLight);
    innerScene.AddChild(directionalLight);
    
    return innerScene;
}

static void ApplyMaterialMode(SceneRenderMode renderMode, SceneRenderable[] standardRenderables, MetaballState metaball)
{
    var shadingMode = renderMode switch
    {
        SceneRenderMode.NormalDebug => SceneMeshShadingMode.Normal,
        SceneRenderMode.DepthDebug => SceneMeshShadingMode.Depth,
        _ => SceneMeshShadingMode.Lit
    };

    var useWireframe = renderMode == SceneRenderMode.Wireframe;

    foreach (var renderable in standardRenderables)
    {
        renderable.SolidMaterial.ShadingMode = shadingMode;
        renderable.Mesh.Material = useWireframe ? renderable.WireMaterial : renderable.SolidMaterial;
    }

    metaball.SolidMaterial.ShadingMode = shadingMode;
    metaball.Mesh.Material = useWireframe ? metaball.WireMaterial : metaball.SolidMaterial;
}

static void ApplyPolygonDetailLevel(PolygonDetailLevel detailLevel, SceneRenderable[] standardRenderables, MetaballState metaball)
{
    foreach (var renderable in standardRenderables)
    {
        renderable.Mesh.Geometry = renderable.PolygonDetail.Get(detailLevel);
        renderable.ApplyDetailLevel?.Invoke(detailLevel);
    }

    metaball.SetDetailLevel(detailLevel);
}

static void ApplySceneContent(
    SceneClass scene,
    SceneRenderable[] standardRenderables,
    MetaballState metaball,
    SceneContentMode contentMode)
{
    scene.RemoveChild(metaball.Mesh);
    foreach (var renderable in standardRenderables)
    {
        scene.RemoveChild(renderable.Mesh);
    }

    if (contentMode == SceneContentMode.Metaball)
    {
        if (!scene.Children.Contains(metaball.Mesh))
        {
            scene.AddChild(metaball.Mesh);
        }

        return;
    }

    foreach (var renderable in standardRenderables)
    {
        var include = contentMode switch
        {
            SceneContentMode.WaveCloth => renderable.IsWaveCloth,
            _ => !renderable.IsWaveCloth
        };

        if (include && !scene.Children.Contains(renderable.Mesh))
        {
            scene.AddChild(renderable.Mesh);
        }
    }
}

static void ApplyLightingSetup(
    bool cinematicLighting,
    SceneAmbientLight ambient,
    SceneDirectionalLight key,
    SceneDirectionalLight fill,
    SceneRenderable[] renderables,
    MetaballState metaball)
{
    if (cinematicLighting)
    {
        ambient.Color = new Vector3(1.0f, 0.95f, 0.90f);
        ambient.Intensity = 0.22f;

        key.Color = new Vector3(1.0f, 0.92f, 0.78f);
        key.Intensity = 1.00f;
        key.Rotation = Quaternion.FromEulerAngles(-0.65f, 0.80f, 0.0f);

        fill.Color = new Vector3(0.70f, 0.82f, 1.0f);
        fill.Intensity = 0.45f;
        fill.Rotation = Quaternion.FromEulerAngles(-0.35f, -1.20f, 0.0f);

        renderables[0].SolidMaterial.Color = new Vector3(0.95f, 0.40f, 0.20f);
        renderables[1].SolidMaterial.Color = new Vector3(0.20f, 0.75f, 0.95f);
        renderables[2].SolidMaterial.Color = new Vector3(0.35f, 0.90f, 0.40f);
        renderables[3].SolidMaterial.Color = new Vector3(0.95f, 0.88f, 0.50f);
        metaball.SolidMaterial.Color = new Vector3(0.78f, 0.62f, 0.95f);
        return;
    }

    ambient.Color = new Vector3(1.0f, 1.0f, 1.0f);
    ambient.Intensity = 0.70f;

    key.Color = new Vector3(1.0f, 1.0f, 1.0f);
    key.Intensity = 0.30f;
    key.Rotation = Quaternion.FromEulerAngles(-0.45f, 0.60f, 0.0f);

    fill.Color = new Vector3(1.0f, 1.0f, 1.0f);
    fill.Intensity = 0.25f;
    fill.Rotation = Quaternion.FromEulerAngles(-0.35f, -0.80f, 0.0f);

    renderables[0].SolidMaterial.Color = new Vector3(0.72f, 0.58f, 0.52f);
    renderables[1].SolidMaterial.Color = new Vector3(0.52f, 0.62f, 0.70f);
    renderables[2].SolidMaterial.Color = new Vector3(0.58f, 0.70f, 0.56f);
    renderables[3].SolidMaterial.Color = new Vector3(0.75f, 0.72f, 0.56f);
    metaball.SolidMaterial.Color = new Vector3(0.64f, 0.60f, 0.70f);
}

static void UpdateCameraTransforms(
    (string Label, SceneCamera Camera)[] cameras,
    float orbitYaw,
    float orbitRadius,
    float orbitHeight)
{
    foreach (var (_, camera) in cameras)
    {
        var position = new Vector3(
            MathF.Sin(orbitYaw) * orbitRadius,
            orbitHeight,
            MathF.Cos(orbitYaw) * orbitRadius);

        camera.Position = position;
        camera.Rotation = LookAtRotation(position, Vector3.Zero);

        if (camera is SceneOrthographicCamera orthographicCamera)
        {
            var size = MathF.Max(8.0f, orbitRadius * 0.90f);
            orthographicCamera.Width = size;
            orthographicCamera.Height = size;
        }
        else if (camera is SceneIsometricCamera isometricCamera)
        {
            isometricCamera.Size = MathF.Max(8.0f, orbitRadius * 0.95f);
        }
        else if (camera is ScenePerspectiveCamera perspectiveCamera)
        {
            perspectiveCamera.FieldOfView = MathF.PI / 2.6f;
        }
    }
}

static Quaternion LookAtRotation(Vector3 from, Vector3 target)
{
    var direction = (target - from).Normalized;
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    var pitch = MathF.Asin(direction.Y);
    return Quaternion.FromEulerAngles(pitch, yaw, 0.0f);
}

static SceneBufferGeometry CreateCubeGeometry()
{
    var positions = new float[]
    {
        -1, -1, -1,  1, -1, -1,  1,  1, -1, -1,  1, -1,
        -1, -1,  1,  1, -1,  1,  1,  1,  1, -1,  1,  1
    };

    var indices = new uint[]
    {
        4, 5, 6, 4, 6, 7,
        0, 2, 1, 0, 3, 2,
        0, 4, 7, 0, 7, 3,
        1, 2, 6, 1, 6, 5,
        3, 7, 6, 3, 6, 2,
        0, 1, 5, 0, 5, 4
    };

    var geometry = new SceneBufferGeometry();
    geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
    geometry.SetIndices(indices);
    return geometry;
}

static SceneBufferGeometry CreatePlaneGeometry(float width = 2.0f, float height = 2.0f)
{
    var hw = width * 0.5f;
    var hh = height * 0.5f;
    var positions = new float[]
    {
        -hw, 0, -hh,  hw, 0, -hh,  hw, 0,  hh, -hw, 0,  hh
    };
    
    var uvs = new float[]
    {
        0, 0,  1, 0,  1, 1,  0, 1
    };
    
    var indices = new uint[] { 0, 2, 1, 0, 3, 2 };
    
    var geometry = new SceneBufferGeometry();
    geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
    geometry.SetAttribute("uv", new SceneBufferAttribute("uv", uvs, 2));
    geometry.SetIndices(indices);
    return geometry;
}

static SceneTexture2D CreateTestTexture(int width = 64, int height = 64)
{
    var texture = new SceneTexture2D(width, height);
    var pixels = texture.GetPixels();
    
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            var idx = (y * width) + x;
            
            // Create a simple checkerboard pattern with red, blue, green colors
            var checkerSize = 8;
            var cx = (x / checkerSize) % 2;
            var cy = (y / checkerSize) % 2;
            var checker = (cx + cy) % 2;
            
            uint color = checker == 0
                ? 0xFF0000FF  // Red (RGBA format)
                : 0x0000FFFF; // Blue
            
            pixels[idx] = color;
        }
    }
    
    texture.SetPixels(pixels);
    return texture;
}

static SceneTexture2D CreateTestTexture2(int width = 64, int height = 64)
{
    var texture = new SceneTexture2D(width, height);
    var pixels = texture.GetPixels();
    
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            var idx = (y * width) + x;
            
            // Create a gradient pattern
            var r = (uint)((x * 255) / width);
            var g = (uint)((y * 255) / height);
            var b = 128u;
            var a = 255u;
            
            uint color = (r << 24) | (g << 16) | (b << 8) | a;
            pixels[idx] = color;
        }
    }
    
    texture.SetPixels(pixels);
    return texture;
}

static SceneTexture2D RenderSceneWidgetToTexture(SceneWidget widget, int width = 128, int height = 128)
{
    // Render any widget (including SceneWidget) by rendering to terminal and converting to RGB texture
    var texture = TerminalTextureRenderer.RenderToTexture(widget, width, height);
    return texture;
}

static (SceneMesh cube, SceneMesh torus, SceneMesh cylinder) GetInnerSceneMeshes(SceneClass scene)
{
    // Extract the inner scene meshes from the scene
    var meshes = scene.Children.OfType<SceneMesh>().ToList();
    var cube = meshes.FirstOrDefault(m => m.Name == "Inner Cube") ?? throw new InvalidOperationException("Inner Cube not found");
    var torus = meshes.FirstOrDefault(m => m.Name == "Inner Torus") ?? throw new InvalidOperationException("Inner Torus not found");
    var cylinder = meshes.FirstOrDefault(m => m.Name == "Inner Cylinder") ?? throw new InvalidOperationException("Inner Cylinder not found");
    return (cube, torus, cylinder);
}

static SceneBufferGeometry CreateCylinderGeometry(float radius, float height, int segments)
{
    var positions = new List<float>();
    var indices = new List<uint>();
    var halfHeight = height * 0.5f;

    for (var i = 0; i < segments; i++)
    {
        var angle = (i * MathF.PI * 2.0f) / segments;
        var x = MathF.Cos(angle) * radius;
        var z = MathF.Sin(angle) * radius;

        positions.Add(x); positions.Add(-halfHeight); positions.Add(z); // bottom
        positions.Add(x); positions.Add(halfHeight); positions.Add(z);  // top
    }

    var bottomCenterIndex = (uint)(positions.Count / 3);
    positions.Add(0); positions.Add(-halfHeight); positions.Add(0);

    var topCenterIndex = (uint)(positions.Count / 3);
    positions.Add(0); positions.Add(halfHeight); positions.Add(0);

    for (var i = 0; i < segments; i++)
    {
        var next = (i + 1) % segments;
        var b0 = (uint)(i * 2);
        var t0 = b0 + 1;
        var b1 = (uint)(next * 2);
        var t1 = b1 + 1;

        // side
        indices.Add(b0); indices.Add(t0); indices.Add(t1);
        indices.Add(b0); indices.Add(t1); indices.Add(b1);

        // bottom cap
        indices.Add(bottomCenterIndex); indices.Add(b1); indices.Add(b0);

        // top cap
        indices.Add(topCenterIndex); indices.Add(t0); indices.Add(t1);
    }

    var geometry = new SceneBufferGeometry();
    geometry.SetAttribute("position", new SceneBufferAttribute("position", [.. positions], 3));
    geometry.SetIndices([.. indices]);
    return geometry;
}

static SceneBufferGeometry CreateTorusGeometry(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
{
    var positions = new List<float>(majorSegments * minorSegments * 3);
    var indices = new List<uint>(majorSegments * minorSegments * 6);

    for (var i = 0; i < majorSegments; i++)
    {
        var u = (i * MathF.PI * 2.0f) / majorSegments;
        var cosU = MathF.Cos(u);
        var sinU = MathF.Sin(u);

        for (var j = 0; j < minorSegments; j++)
        {
            var v = (j * MathF.PI * 2.0f) / minorSegments;
            var cosV = MathF.Cos(v);
            var sinV = MathF.Sin(v);

            var x = (majorRadius + (minorRadius * cosV)) * cosU;
            var y = minorRadius * sinV;
            var z = (majorRadius + (minorRadius * cosV)) * sinU;

            positions.Add(x);
            positions.Add(y);
            positions.Add(z);
        }
    }

    for (var i = 0; i < majorSegments; i++)
    {
        var nextI = (i + 1) % majorSegments;
        for (var j = 0; j < minorSegments; j++)
        {
            var nextJ = (j + 1) % minorSegments;

            var a = (uint)((i * minorSegments) + j);
            var b = (uint)((nextI * minorSegments) + j);
            var c = (uint)((nextI * minorSegments) + nextJ);
            var d = (uint)((i * minorSegments) + nextJ);

            indices.Add(a); indices.Add(b); indices.Add(c);
            indices.Add(a); indices.Add(c); indices.Add(d);
        }
    }

    var geometry = new SceneBufferGeometry();
    geometry.SetAttribute("position", new SceneBufferAttribute("position", [.. positions], 3));
    geometry.SetIndices([.. indices]);
    return geometry;
}

static FabricWaveDetail CreateFabricWaveDetail(float width, float depth, int segmentsX, int segmentsZ)
{
    var basePoints = new List<Vector2>();
    var positions = new List<float>();
    var uvs = new List<float>();
    var indices = new List<uint>();

    for (var z = 0; z <= segmentsZ; z++)
    {
        var vz = (float)z / segmentsZ;
        var pz = ((vz * 2.0f) - 1.0f) * (depth * 0.5f);
        for (var x = 0; x <= segmentsX; x++)
        {
            var vx = (float)x / segmentsX;
            var px = ((vx * 2.0f) - 1.0f) * (width * 0.5f);
            basePoints.Add(new Vector2(px, pz));
            positions.Add(px);
            positions.Add(0.0f);
            positions.Add(pz);
            uvs.Add(vx);
            uvs.Add(vz);
        }
    }

    var rowStride = segmentsX + 1;
    for (var z = 0; z < segmentsZ; z++)
    {
        for (var x = 0; x < segmentsX; x++)
        {
            var a = (uint)((z * rowStride) + x);
            var b = a + 1;
            var c = (uint)(((z + 1) * rowStride) + x + 1);
            var d = c - 1;

            indices.Add(a); indices.Add(d); indices.Add(c);
            indices.Add(a); indices.Add(c); indices.Add(b);
        }
    }

    var geometry = new SceneBufferGeometry();
    var positionAttribute = new SceneBufferAttribute("position", [.. positions], 3);
    geometry.SetAttribute("position", positionAttribute);
    geometry.SetAttribute("uv", new SceneBufferAttribute("uv", [.. uvs], 2));
    geometry.SetIndices([.. indices]);

    return new FabricWaveDetail(geometry, positionAttribute, [.. basePoints]);
}

static MetaballState CreateMetaballState()
{
    const float baseRadius = 1.5f;
    var details = new PolygonDetailProfile<MetaballGeometryDetail>(
        High: CreateMetaballGeometryDetail(baseRadius, latitudeSegments: 20, longitudeSegments: 26),
        Medium: CreateMetaballGeometryDetail(baseRadius, latitudeSegments: 12, longitudeSegments: 16),
        Low: CreateMetaballGeometryDetail(baseRadius, latitudeSegments: 7, longitudeSegments: 10));

    var wire = new SceneLineBasicMaterial(new Vector3(0.95f, 0.65f, 1.0f));
    var solid = new SceneMeshMaterial(new Vector3(0.75f, 0.55f, 0.95f));
    var mesh = new SceneMesh(details.High.Geometry, solid, "Metaball");

    return new MetaballState(mesh, wire, solid, details, baseRadius);
}

static MetaballGeometryDetail CreateMetaballGeometryDetail(float baseRadius, int latitudeSegments, int longitudeSegments)
{
    var unitDirections = new List<Vector3>();
    var positions = new List<float>();
    var indices = new List<uint>();

    for (var lat = 0; lat <= latitudeSegments; lat++)
    {
        var v = (float)lat / latitudeSegments;
        var phi = v * MathF.PI;
        var y = MathF.Cos(phi);
        var ringRadius = MathF.Sin(phi);

        for (var lon = 0; lon <= longitudeSegments; lon++)
        {
            var u = (float)lon / longitudeSegments;
            var theta = u * MathF.PI * 2.0f;

            var x = ringRadius * MathF.Cos(theta);
            var z = ringRadius * MathF.Sin(theta);
            var direction = new Vector3(x, y, z).Normalized;

            unitDirections.Add(direction);
            positions.Add(direction.X * baseRadius);
            positions.Add(direction.Y * baseRadius);
            positions.Add(direction.Z * baseRadius);
        }
    }

    var stride = longitudeSegments + 1;
    for (var lat = 0; lat < latitudeSegments; lat++)
    {
        for (var lon = 0; lon < longitudeSegments; lon++)
        {
            var a = (uint)((lat * stride) + lon);
            var b = (uint)(((lat + 1) * stride) + lon);
            var c = b + 1;
            var d = a + 1;

            indices.Add(a); indices.Add(b); indices.Add(c);
            indices.Add(a); indices.Add(c); indices.Add(d);
        }
    }

    var geometry = new SceneBufferGeometry();
    var positionAttribute = new SceneBufferAttribute("position", [.. positions], 3);
    geometry.SetAttribute("position", positionAttribute);
    geometry.SetIndices([.. indices]);

    return new MetaballGeometryDetail(geometry, positionAttribute, [.. unitDirections]);
}

static void UpdateMetaball(MetaballState metaball, float timeSeconds)
{
    var sourceA = new Vector3(MathF.Sin(timeSeconds * 0.7f), MathF.Cos(timeSeconds * 1.1f), MathF.Sin(timeSeconds * 0.9f)) * 1.0f;
    var sourceB = new Vector3(MathF.Sin(timeSeconds * 1.4f + 1.2f), MathF.Cos(timeSeconds * 0.6f + 0.3f), MathF.Sin(timeSeconds * 1.3f + 2.1f)) * 1.0f;
    var sourceC = new Vector3(MathF.Sin(timeSeconds * 0.9f + 2.6f), MathF.Cos(timeSeconds * 1.6f + 0.7f), MathF.Sin(timeSeconds * 0.5f + 1.4f)) * 1.0f;

    for (var i = 0; i < metaball.UnitDirections.Length; i++)
    {
        var direction = metaball.UnitDirections[i];
        var sample = direction * metaball.BaseRadius;

        var field =
            MetaField(sample, sourceA, 1.15f) +
            MetaField(sample, sourceB, 1.10f) +
            MetaField(sample, sourceC, 1.00f);

        var radius = metaball.BaseRadius + ((field - 1.0f) * 0.16f);
        radius = Math.Clamp(radius, metaball.BaseRadius * 0.72f, metaball.BaseRadius * 1.45f);
        var position = direction * radius;

        metaball.PositionAttribute.SetComponent(i, 0, position.X);
        metaball.PositionAttribute.SetComponent(i, 1, position.Y);
        metaball.PositionAttribute.SetComponent(i, 2, position.Z);
    }
}

static float MetaField(Vector3 sample, Vector3 source, float strength)
{
    var diff = sample - source;
    var distSqr = (diff.X * diff.X) + (diff.Y * diff.Y) + (diff.Z * diff.Z);
    return strength / (distSqr + 0.38f);
}

static void UpdateFabricWave(FabricWaveDetail detail, float timeSeconds)
{
    for (var i = 0; i < detail.BasePoints.Length; i++)
    {
        var point = detail.BasePoints[i];
        var px = point.X;
        var pz = point.Y;

        var rippleA = MathF.Sin((px * 2.8f) - (timeSeconds * 2.6f));
        var rippleB = MathF.Sin((pz * 2.0f) - (timeSeconds * 1.9f));
        var radial = MathF.Sin((((px * px) + (pz * pz)) * 3.5f) - (timeSeconds * 3.1f));
        var height = (rippleA * 0.17f) + (rippleB * 0.11f) + (radial * 0.08f);

        detail.PositionAttribute.SetComponent(i, 0, px);
        detail.PositionAttribute.SetComponent(i, 1, height);
        detail.PositionAttribute.SetComponent(i, 2, pz);
    }
}

public sealed record SceneRenderable(
    SceneMesh Mesh,
    SceneLineBasicMaterial WireMaterial,
    SceneMeshMaterial SolidMaterial,
    Vector3 RotationSpeed,
    PolygonDetailProfile<SceneBufferGeometry> PolygonDetail,
    Action<PolygonDetailLevel>? ApplyDetailLevel = null,
    Action<float>? Update = null)
{
    public bool IsWaveCloth => string.Equals(Mesh.Name, "Wave Cloth", StringComparison.Ordinal);
}

public sealed class MetaballState
{
    private readonly PolygonDetailProfile<MetaballGeometryDetail> _polygonDetail;

    public SceneMesh Mesh { get; }
    public SceneLineBasicMaterial WireMaterial { get; }
    public SceneMeshMaterial SolidMaterial { get; }
    public SceneBufferAttribute PositionAttribute { get; private set; }
    public Vector3[] UnitDirections { get; private set; }
    public float BaseRadius { get; }

    public MetaballState(
        SceneMesh mesh,
        SceneLineBasicMaterial wireMaterial,
        SceneMeshMaterial solidMaterial,
        PolygonDetailProfile<MetaballGeometryDetail> polygonDetail,
        float baseRadius)
    {
        Mesh = mesh;
        WireMaterial = wireMaterial;
        SolidMaterial = solidMaterial;
        _polygonDetail = polygonDetail;
        BaseRadius = baseRadius;
        PositionAttribute = polygonDetail.High.PositionAttribute;
        UnitDirections = polygonDetail.High.UnitDirections;
    }

    public void SetDetailLevel(PolygonDetailLevel detailLevel)
    {
        var detail = _polygonDetail.Get(detailLevel);
        Mesh.Geometry = detail.Geometry;
        PositionAttribute = detail.PositionAttribute;
        UnitDirections = detail.UnitDirections;
    }
}

public sealed record MetaballGeometryDetail(
    SceneBufferGeometry Geometry,
    SceneBufferAttribute PositionAttribute,
    Vector3[] UnitDirections);

public sealed record FabricWaveDetail(
    SceneBufferGeometry Geometry,
    SceneBufferAttribute PositionAttribute,
    Vector2[] BasePoints);

public enum PolygonDetailLevel
{
    High,
    Medium,
    Low
}

public enum SceneContentMode
{
    Primitives,
    Metaball,
    WaveCloth,
    TexturedPlane
}

public enum SceneRenderMode
{
    Wireframe,
    Lit,
    NormalDebug,
    DepthDebug
}

public readonly record struct PolygonDetailProfile<T>(T High, T Medium, T Low)
{
    public T Get(PolygonDetailLevel detailLevel)
    {
        return detailLevel switch
        {
            PolygonDetailLevel.High => High,
            PolygonDetailLevel.Medium => Medium,
            _ => Low
        };
    }
}
