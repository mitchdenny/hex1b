using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// The selected Hex1b default Sixel palette.
/// </summary>
/// <remarks>
/// Registers 0-15 are the DEC VT340 defaults expressed in the DEC 0-100 domain
/// and converted with the same deterministic rounding used by DECGCI. Registers
/// 16-255 extend the VT340 set with the conventional modern 6x6x6 color cube and
/// grayscale ramp so selection without definition is defined for every register
/// inside policy.
/// </remarks>
internal static class SixelDefaultPalette
{
    private static readonly (int R, int G, int B)[] Vt340Percentages =
    [
        (0, 0, 0),
        (20, 20, 80),
        (80, 13, 13),
        (20, 80, 20),
        (80, 20, 80),
        (20, 80, 80),
        (80, 80, 20),
        (53, 53, 53),
        (26, 26, 26),
        (33, 33, 60),
        (60, 26, 26),
        (33, 60, 33),
        (60, 33, 60),
        (33, 60, 60),
        (60, 60, 33),
        (80, 80, 80),
    ];

    private static readonly byte[] CubeLevels = [0, 95, 135, 175, 215, 255];

    /// <summary>
    /// Gets the number of DEC VT340 registers that use hardware default colors.
    /// </summary>
    public const int Vt340RegisterCount = 16;

    /// <summary>
    /// Writes the default palette into <paramref name="destination"/>.
    /// </summary>
    public static void Fill(Span<Rgba32> destination)
    {
        for (var register = 0; register < destination.Length; register++)
        {
            destination[register] = Get(register);
        }
    }

    /// <summary>
    /// Gets the default color for a register index.
    /// </summary>
    public static Rgba32 Get(int register)
    {
        if (register < 0)
        {
            return new Rgba32(0, 0, 0, 255);
        }

        if (register < Vt340RegisterCount)
        {
            var (r, g, b) = Vt340Percentages[register];
            return SixelColorConverter.FromRgbPercent(r, g, b);
        }

        if (register < 232)
        {
            var index = register - 16;
            return new Rgba32(
                CubeLevels[(index / 36) % 6],
                CubeLevels[(index / 6) % 6],
                CubeLevels[index % 6],
                255);
        }

        if (register < 256)
        {
            var level = (byte)(8 + ((register - 232) * 10));
            return new Rgba32(level, level, level, 255);
        }

        return new Rgba32(0, 0, 0, 255);
    }
}

/// <summary>
/// Terminal-scoped, persistent Sixel color registers.
/// </summary>
/// <remarks>
/// Registers survive between Sixel sequences, across alternate-screen
/// transitions, and across DECSTR. Only RIS restores the default palette.
/// </remarks>
internal sealed class SixelColorRegisters
{
    private readonly Rgba32[] _registers;

    public SixelColorRegisters(SixelCompatibilityPolicy? policy = null)
    {
        Policy = policy ?? SixelCompatibilityPolicy.Default;
        _registers = new Rgba32[Policy.ColorRegisterCount];
        SixelDefaultPalette.Fill(_registers);
    }

    private SixelColorRegisters(SixelCompatibilityPolicy policy, Rgba32[] registers)
    {
        Policy = policy;
        _registers = registers;
    }

    /// <summary>
    /// Gets the policy that bounds this register file.
    /// </summary>
    public SixelCompatibilityPolicy Policy { get; }

    /// <summary>
    /// Gets the number of addressable registers.
    /// </summary>
    public int Count => _registers.Length;

    /// <summary>
    /// Determines whether a register number is inside the configured policy.
    /// </summary>
    public bool IsWithinPolicy(int register) => register >= 0 && register < _registers.Length;

    /// <summary>
    /// Gets the current color for an in-policy register.
    /// </summary>
    public Rgba32 Get(int register) => _registers[register];

    /// <summary>
    /// Defines an in-policy register.
    /// </summary>
    public void Define(int register, Rgba32 color) => _registers[register] = color;

    /// <summary>
    /// Restores every register to its default value.
    /// </summary>
    public void Reset() => SixelDefaultPalette.Fill(_registers);

    /// <summary>
    /// Creates an independent copy for private-per-graphic compatibility policies
    /// and for deterministic test inspection.
    /// </summary>
    public SixelColorRegisters Snapshot() => new(Policy, [.. _registers]);
}
