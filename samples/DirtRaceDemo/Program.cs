using System.Diagnostics;
using DirtRaceDemo.Game;
using Hex1b;
using Hex1b.Input;

var game = new RaceGame();

var stopwatch = Stopwatch.StartNew();
var lastSeconds = 0.0f;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(
        _ => { },
        app => ctx =>
            ctx.Interactable(ic =>
            {
                var now = (float)stopwatch.Elapsed.TotalSeconds;
                var deltaSeconds = now - lastSeconds;
                if (deltaSeconds <= 0.0f)
                {
                    deltaSeconds = 1.0f / 60.0f;
                }

                // Clamp large gaps (e.g. after a stall) so physics stays stable.
                deltaSeconds = MathF.Min(deltaSeconds, 0.1f);
                lastSeconds = now;

                game.Update(deltaSeconds);
                return game.BuildView(ic);
            })
            .InputBindings(bindings =>
            {
                Bind(bindings, Hex1bKey.W, game.Input.PressForward, "Accelerate");
                Bind(bindings, Hex1bKey.UpArrow, game.Input.PressForward, "Accelerate");
                Bind(bindings, Hex1bKey.S, game.Input.PressReverse, "Brake / reverse");
                Bind(bindings, Hex1bKey.DownArrow, game.Input.PressReverse, "Brake / reverse");
                Bind(bindings, Hex1bKey.A, game.Input.PressLeft, "Steer left");
                Bind(bindings, Hex1bKey.LeftArrow, game.Input.PressLeft, "Steer left");
                Bind(bindings, Hex1bKey.D, game.Input.PressRight, "Steer right");
                Bind(bindings, Hex1bKey.RightArrow, game.Input.PressRight, "Steer right");
                Bind(bindings, Hex1bKey.Spacebar, game.Input.PressHandbrake, "Handbrake");
                Bind(bindings, Hex1bKey.R, game.Input.RequestReset, "Reset to start");

                bindings.Key(Hex1bKey.Q).Global().Action(_ => app.RequestStop(), "Quit");
                bindings.Key(Hex1bKey.Escape).Global().Action(_ => app.RequestStop(), "Quit");

                void Bind(InputBindingsBuilder b, Hex1bKey key, Action press, string description) =>
                    b.Key(key).Global().Action(_ =>
                    {
                        press();
                        app.Invalidate();
                    }, description);
            }))
    .Build();

await terminal.RunAsync();
