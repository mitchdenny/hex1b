using Hex1b.Surfaces;

namespace Hex1b.Sixel;

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
        SixelDefaultPalette.Fill(_registers, Policy.InitialPalette);
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
    public void Reset() => SixelDefaultPalette.Fill(_registers, Policy.InitialPalette);

    /// <summary>
    /// Creates an independent copy for private-per-graphic compatibility policies
    /// and for deterministic test inspection.
    /// </summary>
    public SixelColorRegisters Snapshot() => new(Policy, [.. _registers]);
}
