// Wrap an existing font and override TryGetGlyph to substitute characters.
public sealed class UppercaseFont : FigletFont
{
    public UppercaseFont(FigletFont inner) : base(inner) { }

    public override bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
    {
        // Force lowercase letters to use the uppercase glyph.
        if (codePoint >= 'a' && codePoint <= 'z')
        {
            codePoint -= 'a' - 'A';
        }
        return base.TryGetGlyph(codePoint, out glyph);
    }
}

var font = new UppercaseFont(FigletFonts.Standard);
ctx.FigletText("hello").Font(font); // renders as "HELLO"
