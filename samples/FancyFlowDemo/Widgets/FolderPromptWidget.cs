using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Flow;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Step 3 — picks an output folder. The predictor walks the actual filesystem
/// from the current working directory: it splits the typed text into a
/// directory portion and a partial leaf name, then suggests the first matching
/// entry in that directory. A second line shows the fully-resolved absolute
/// path the user is currently pointing at.
/// </summary>
internal sealed record FolderPromptWidget(
    FlowStepContext Step,
    FancyFlowCancellation Cancel,
    FancyFlowSelections Selections) : Hex1bWidget
{
    private static readonly Hex1bColor PathColor = Hex1bColor.FromRgb(140, 130, 160);

    private sealed class FolderState
    {
        public string CurrentText = "";
    }

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new FolderState { CurrentText = Selections.Folder });

        var textBox = ctx.TextBox(Selections.Folder)
            .Predict(async (text, ct) =>
            {
                await Task.Yield();
                return PredictFromFilesystem(text);
            })
            .OnTextChanged(e =>
            {
                state.CurrentText = e.NewText;
                Step.Step.Invalidate();
            })
            .OnSubmit(e =>
            {
                Selections.Folder = string.IsNullOrWhiteSpace(e.Text) ? Selections.Folder : e.Text;
                Step.Step.Complete(y => TombstoneFactory.Build(y, $"✓ Folder: {Selections.Folder}"));
            })
            .FillWidth();

        var resolved = ResolvePath(state.CurrentText);

        // Wrap the textbox + resolved-path line in the shared 1/3-width
        // rounded yellow border (matches the framing used by the list and
        // toggle prompts).
        var content = PromptBorder.Wrap(ctx, ctx.VStack(v =>
        [
            textBox,
            v.ThemePanel(
                p => p.Set(GlobalTheme.ForegroundColor, PathColor),
                v.Text($"→ {resolved}")),
        ]));

        return ctx.TemplatePrompt(
                stepNumber: 3,
                title: "Pick an output folder",
                description: "Type a path. Right arrow accepts the dim suggestion. Enter to confirm.",
                content: content)
            .ExitOnCtrlC(Cancel, Step);
    }

    private static string ResolvePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Directory.GetCurrentDirectory();
        }

        try
        {
            return Path.GetFullPath(input);
        }
        catch
        {
            return input;
        }
    }

    /// <summary>
    /// Splits the typed text into a directory portion and a partial leaf name,
    /// enumerates the directory, and returns the suffix of the first matching
    /// entry. Directories are preferred and a trailing separator is included
    /// so successive Right-arrow accepts walk down the tree naturally.
    /// </summary>
    private static string? PredictFromFilesystem(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        try
        {
            string dirPart;
            string leafPart;

            // If the input ends in a separator, treat it as "list this directory, no filter".
            var lastChar = input[^1];
            if (lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar)
            {
                dirPart = input;
                leafPart = "";
            }
            else
            {
                dirPart = Path.GetDirectoryName(input) ?? "";
                leafPart = Path.GetFileName(input);
            }

            var searchDir = string.IsNullOrEmpty(dirPart)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(dirPart);

            if (!Directory.Exists(searchDir))
            {
                return null;
            }

            // Directories first, then files, both alphabetically.
            var matches = Directory.EnumerateFileSystemEntries(searchDir)
                .Select(p => new
                {
                    Name = Path.GetFileName(p),
                    IsDir = Directory.Exists(p),
                })
                .Where(e => !string.IsNullOrEmpty(e.Name)
                    && e.Name.StartsWith(leafPart, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.IsDir)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 0)
            {
                return null;
            }

            var best = matches[0];
            var suffix = best.Name[leafPart.Length..];
            if (best.IsDir)
            {
                suffix += Path.DirectorySeparatorChar;
            }
            return suffix.Length == 0 ? null : suffix;
        }
        catch
        {
            return null;
        }
    }
}

