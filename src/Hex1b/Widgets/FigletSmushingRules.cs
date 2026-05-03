namespace Hex1b.Widgets;

/// <summary>
/// Internal helpers that resolve the per-cell outcome of FIGfont smushing for a single
/// pair of sub-characters. Implements both the six controlled horizontal rules and the
/// five vertical rules from the FIGfont 2.0 specification, plus the universal smushing
/// algorithm used when no controlled rule bits are set.
/// </summary>
internal static class FigletSmushingRules
{
    // Bit flags from full_layout (post-shift for vertical so they sit in bits 1..16).
    public const int HorizontalRuleEqual      = 1;
    public const int HorizontalRuleUnderscore = 2;
    public const int HorizontalRuleHierarchy  = 4;
    public const int HorizontalRuleOpposite   = 8;
    public const int HorizontalRuleBigX       = 16;
    public const int HorizontalRuleHardblank  = 32;

    public const int VerticalRuleEqual          = 1;
    public const int VerticalRuleUnderscore     = 2;
    public const int VerticalRuleHierarchy      = 4;
    public const int VerticalRuleHorizontalLine = 8;
    public const int VerticalRuleVerticalLine   = 16;

    private const string UnderscoreReplacers = "|/\\[]{}()<>";

    /// <summary>
    /// Attempts to horizontally smush two adjacent sub-characters together using the active rule
    /// set. Returns <see langword="false"/> if the cells cannot be merged; the caller should then
    /// reduce the trial overlap by one column.
    /// </summary>
    /// <param name="left">The right-most cell of the existing output block at this row.</param>
    /// <param name="right">The left-most cell of the incoming glyph at this row.</param>
    /// <param name="hardblank">The font's hardblank sub-character.</param>
    /// <param name="rules">
    /// Bitmask of horizontal smushing rules from <see cref="FigletFont.HorizontalSmushingRules"/>.
    /// When the bitmask is 0 (no controlled rules active) universal smushing is performed instead.
    /// </param>
    /// <param name="result">The resolved sub-character if smushing succeeded.</param>
    public static bool TrySmushHorizontal(char left, char right, char hardblank, int rules, out char result)
    {
        // Spaces (blanks) always yield the OTHER character. This rule applies in BOTH controlled
        // and universal smushing, regardless of which rule bits are set.
        if (left == ' ')
        {
            result = right;
            return true;
        }
        if (right == ' ')
        {
            result = left;
            return true;
        }

        // Universal smushing: invoked when NO controlled rule bits are set in `rules`.
        // (Plan note: controlled and universal are distinct algorithms — controlled never falls
        //  back to universal, and universal is NOT just "all rules on".)
        if (rules == 0)
        {
            return TryUniversalHorizontal(left, right, hardblank, out result);
        }

        // Controlled smushing: hardblanks may smush ONLY via Rule 6.
        if (left == hardblank || right == hardblank)
        {
            if ((rules & HorizontalRuleHardblank) != 0 && left == hardblank && right == hardblank)
            {
                result = hardblank;
                return true;
            }
            result = '\0';
            return false;
        }

        // Try each controlled rule in spec order.
        if ((rules & HorizontalRuleEqual) != 0 && left == right)
        {
            result = left;
            return true;
        }

        if ((rules & HorizontalRuleUnderscore) != 0)
        {
            if (left == '_' && UnderscoreReplacers.IndexOf(right) >= 0)
            {
                result = right;
                return true;
            }
            if (right == '_' && UnderscoreReplacers.IndexOf(left) >= 0)
            {
                result = left;
                return true;
            }
        }

        if ((rules & HorizontalRuleHierarchy) != 0)
        {
            var lc = HierarchyClass(left);
            var rc = HierarchyClass(right);
            if (lc >= 0 && rc >= 0 && lc != rc)
            {
                result = lc > rc ? left : right;
                return true;
            }
        }

        if ((rules & HorizontalRuleOpposite) != 0)
        {
            if (IsOppositePair(left, right))
            {
                result = '|';
                return true;
            }
        }

        if ((rules & HorizontalRuleBigX) != 0)
        {
            if (left == '/' && right == '\\') { result = '|'; return true; }
            if (left == '\\' && right == '/') { result = 'Y'; return true; }
            if (left == '>' && right == '<')  { result = 'X'; return true; }
        }

        result = '\0';
        return false;
    }

    private static bool TryUniversalHorizontal(char left, char right, char hardblank, out char result)
    {
        // (Blanks were already handled by the caller.)
        // Hardblank handling per the spec: a hardblank "stops" visible chars only AFTER its
        // location is occupied. In other words:
        //   - hardblank vs visible: REJECT (the hardblank still protects its column)
        //   - hardblank vs hardblank: yield a single hardblank
        // Two visibles: the right (later) glyph wins.
        if (left == hardblank && right == hardblank)
        {
            result = hardblank;
            return true;
        }
        if (left == hardblank || right == hardblank)
        {
            result = '\0';
            return false;
        }

        // Two visible non-blank, non-hardblank chars: right wins.
        result = right;
        return true;
    }

    /// <summary>
    /// Attempts to vertically smush two stacked sub-characters together (the bottom row of the
    /// upper block over the top row of the lower block) using the active vertical rule set.
    /// Returns <see langword="false"/> if the cells cannot be merged.
    /// </summary>
    /// <param name="upper">The bottom-row sub-character from the upper output block.</param>
    /// <param name="lower">The top-row sub-character from the lower glyph.</param>
    /// <param name="hardblank">The font's hardblank. Hardblanks behave like blanks in vertical operations.</param>
    /// <param name="rules">Bitmask of vertical smushing rules (bits 1..16, post-shift).</param>
    /// <param name="result">The resolved sub-character.</param>
    public static bool TrySmushVertical(char upper, char lower, char hardblank, int rules, out char result)
    {
        // Per the spec, hardblanks act exactly like blanks in vertical operations.
        var u = upper == hardblank ? ' ' : upper;
        var l = lower == hardblank ? ' ' : lower;

        if (u == ' ')
        {
            result = l;
            return true;
        }
        if (l == ' ')
        {
            result = u;
            return true;
        }

        // Universal vertical smushing: right (later, i.e. lower) wins. (Blanks above already
        // handled.) The spec explicitly notes hardblanks have NO special role vertically.
        if (rules == 0)
        {
            result = l;
            return true;
        }

        if ((rules & VerticalRuleEqual) != 0 && u == l)
        {
            result = u;
            return true;
        }

        if ((rules & VerticalRuleUnderscore) != 0)
        {
            if (u == '_' && UnderscoreReplacers.IndexOf(l) >= 0)
            {
                result = l;
                return true;
            }
            if (l == '_' && UnderscoreReplacers.IndexOf(u) >= 0)
            {
                result = u;
                return true;
            }
        }

        if ((rules & VerticalRuleHierarchy) != 0)
        {
            var uc = HierarchyClass(u);
            var lc = HierarchyClass(l);
            if (uc >= 0 && lc >= 0 && uc != lc)
            {
                result = uc > lc ? u : l;
                return true;
            }
        }

        if ((rules & VerticalRuleHorizontalLine) != 0)
        {
            if ((u == '-' && l == '_') || (u == '_' && l == '-'))
            {
                result = '=';
                return true;
            }
        }

        // Vertical rule 5 (super-smushing of '|') is handled at the row-level orchestration in
        // the renderer because it can collapse multiple rows. The per-cell merge for two '|'
        // is exactly equal-character smushing, which is covered when rule 1 is also active. The
        // renderer is responsible for the multi-row collapse.

        result = '\0';
        return false;
    }

    /// <summary>
    /// Returns the spec's hierarchy class for <paramref name="c"/>:
    /// <c>|</c>=0, <c>/\</c>=1, <c>[]</c>=2, <c>{}</c>=3, <c>()</c>=4, <c>&lt;&gt;</c>=5.
    /// Returns -1 for characters that are not part of the hierarchy.
    /// </summary>
    private static int HierarchyClass(char c) => c switch
    {
        '|' => 0,
        '/' or '\\' => 1,
        '[' or ']' => 2,
        '{' or '}' => 3,
        '(' or ')' => 4,
        '<' or '>' => 5,
        _ => -1,
    };

    private static bool IsOppositePair(char a, char b) =>
        (a == '[' && b == ']') || (a == ']' && b == '[') ||
        (a == '{' && b == '}') || (a == '}' && b == '{') ||
        (a == '(' && b == ')') || (a == ')' && b == '(');
}
