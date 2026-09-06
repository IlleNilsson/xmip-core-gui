namespace Xmip.Gui.Surface;

/// <summary>
/// The role this surface runs as (ADR-0009). It is <b>assigned, not chosen</b>:
/// a person cannot promote themselves in the UI — an Observer stays an Observer.
/// Until a real identity gates it (ADR-0022/ADR-0027), the role is assigned at
/// launch (configuration), defaulting to the least-privileged Observer; the web
/// surface is always Observer, being monitoring only (ADR-0014).
/// </summary>
public sealed class RoleContext(Role role)
{
    /// <summary>The assigned role. Read-only for the life of the surface.</summary>
    public Role Role { get; } = role;

    /// <summary>Whether this surface may reach configuration.</summary>
    public bool MayConfigure()
    {
        return Role.MayConfigure();
    }

    /// <summary>
    /// The role named in configuration, or Observer when absent or unrecognised —
    /// the safe default, so a misconfiguration never grants privilege.
    /// </summary>
    public static Role Parse(string? text)
    {
        return text?.Trim().ToLowerInvariant() switch
        {
            "operator" => Role.Operator,
            "developer" => Role.Developer,
            _ => Role.Observer,
        };
    }
}
