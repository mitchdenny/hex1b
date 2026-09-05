namespace Hex1b.Tokens;

/// <summary>
/// Represents a Primary Device Attributes (DA1) request: <c>CSI c</c> or <c>CSI 0 c</c>.
/// </summary>
/// <remarks>
/// The hosted workload sends this to ask what kind of terminal it is talking to. The
/// authoritative reply shape is <c>CSI ? Pn (; Pn)* c</c>, where DEC parameter
/// <c>4</c> anywhere after the device-class parameter declares Sixel graphics support.
/// Whether <see cref="Hex1bTerminal"/> answers this token at all depends on query
/// ownership: it stays silent when the active presentation's
/// <see cref="IHex1bTerminalPresentationAdapter.AnswersProtocolQueriesDirectly"/> is
/// <see langword="true"/> (a real upstream terminal will already answer directly), and
/// otherwise synthesizes a reply from its own authoritative capability model.
/// </remarks>
public sealed record DeviceAttributesQueryToken : AnsiToken;
