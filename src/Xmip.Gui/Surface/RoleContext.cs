using Microsoft.Extensions.Configuration;

namespace Xmip.Gui.Surface;

/// <summary>
/// The role this surface runs as (ADR-0009). It is <b>assigned, not chosen</b>:
/// a person cannot promote themselves in the UI — an Observer stays an Observer.
/// Both hosts take it by one rule, <see cref="Assigned"/>, and offer by it
/// what it may do (ADR-0014 and ADR-0052, amendments 2026-09-14): an Observer
/// watches, an Operator also pauses, resumes and configures, a Developer also
/// opens configuration.
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

    /// <summary>Whether this surface may pause and resume the running estate.</summary>
    public bool MayOperate()
    {
        return Role.MayOperate();
    }

    /// <summary>The configuration key a run states a role under, in the host's
    /// <c>[Xmip]</c> table.</summary>
    public const string ConfigurationKey = "Xmip:Role";

    /// <summary>The environment variable a run states a role in.</summary>
    public const string EnvironmentVariable = "XMIP_ROLE";

    /// <summary>
    /// The role a host runs as, by one rule for the web and the desktop
    /// (ADR-0009, amendment 2026-09-14): a role the run states —
    /// <see cref="ConfigurationKey"/>, else <see cref="EnvironmentVariable"/>
    /// — is the role, and one it states wrongly is Observer, so a
    /// misstatement grants nothing. A run that states none has its role from
    /// the directory, and no directory is configured yet: the Playground's is
    /// the fake directory that allows the tester, who holds every role —
    /// Developer, which includes the rest. The gate that asks a real
    /// directory replaces that answer and nothing else.
    /// </summary>
    public static Role Assigned(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? stated = configuration[ConfigurationKey]
            ?? Environment.GetEnvironmentVariable(EnvironmentVariable);

        return string.IsNullOrWhiteSpace(stated) ? Role.Developer : Parse(stated);
    }

    /// <summary>
    /// The role named in configuration, or Observer when unrecognised — the
    /// safe default, so a misconfiguration never grants privilege.
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
