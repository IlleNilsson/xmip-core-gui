namespace Xmip.Gui.Surface;

/// <summary>
/// A security role — what a person may do in Xmip (ADR-0009), least first:
/// Observer watches; Operator also configures; Developer also builds. Each
/// includes what the one before it can do. Colours are never roles; this is who
/// is at the keyboard.
///
/// Not yet enforced by a real identity (ADR-0009 amendment, ADR-0022/ADR-0027
/// gate that). The surface uses it to show the concept and shape what is offered
/// — an Observer is shown monitoring only, an Operator also configuration.
/// </summary>
public enum Role
{
    /// <summary>Watches. Monitoring surfaces only; changes nothing.</summary>
    Observer = 0,

    /// <summary>Also configures — the desktop's Configure surface.</summary>
    Operator = 1,

    /// <summary>Also builds. Everything an Operator has, plus development.</summary>
    Developer = 2,
}

/// <summary>Helpers over <see cref="Role"/>, so the surfaces agree on what each may do.</summary>
public static class Roles
{
    /// <summary>Whether the role may reach configuration (Operator and up).</summary>
    public static bool MayConfigure(this Role role)
    {
        return role >= Role.Operator;
    }

    /// <summary>
    /// Whether the role may act on the running estate — pause and resume a
    /// Location, a host or a node (Operator and up). An Observer only watches.
    /// </summary>
    public static bool MayOperate(this Role role)
    {
        return role >= Role.Operator;
    }

    /// <summary>The role's own name, for a badge.</summary>
    public static string Label(this Role role)
    {
        return role.ToString();
    }
}
