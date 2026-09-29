namespace Xmip.Gui.Surface;

/// <summary>
/// A security role — what a person may do in Xmip (ADR-0009), least first:
/// Observer watches; Operator also acts and configures; Developer also builds. Each
/// includes what the one before it can do. Colours are never roles; this is who
/// is at the keyboard.
///
/// Not yet enforced by a real identity (ADR-0009 amendment, ADR-0022/ADR-0027
/// gate that). The surface uses it to show the concept and shape what is offered
/// — an Observer is shown monitoring only, an Operator also the acts — pause,
/// resume, remove — and configuration.
/// </summary>
public enum Role
{
    /// <summary>Watches. Monitoring surfaces only; changes nothing.</summary>
    Observer = 0,

    /// <summary>Also acts — pauses, resumes, removes — and configures.</summary>
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
    /// Location, a host or a node, and pause, resume and remove an Event
    /// subscription (Operator and up). An Observer only watches.
    /// </summary>
    public static bool MayOperate(this Role role)
    {
        return role >= Role.Operator;
    }

    /// <summary>What the role may do, in the words every host's badge uses:
    /// the one description, so the web and the desktop say the same.</summary>
    public static string Describe(this Role role)
    {
        return role switch
        {
            Role.Observer => "watches — monitoring only",
            Role.Operator => "also acts and configures",
            Role.Developer => "also builds",
            _ => string.Empty,
        };
    }
}
