using System.Globalization;

namespace Xmip.Gui.Surface;

/// <summary>
/// A time as the person at the keyboard reads it: kept in UTC everywhere
/// beneath, and shown in the zone and the culture of the machine the view
/// is drawn for, with the offset said so nobody takes it for UTC. The UTC
/// itself is <see cref="Utc"/>, for a title beside it.
/// </summary>
/// <remarks>
/// On the desktop and the phone that machine is the person's own. The web
/// host draws on the server, which knows neither the browser's zone — only
/// the browser could say it, and the GUI carries no script of its own — nor
/// a culture while it builds with invariant globalization
/// (<c>Directory.Build.props</c>), so there it says the server's zone and the
/// invariant culture, the offset always beside them.
/// </remarks>
public static class PersonTime
{
    /// <summary><paramref name="at"/> where this machine is, in its culture.</summary>
    public static string Said(DateTimeOffset at)
    {
        return Said(at, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
    }

    /// <summary><paramref name="at"/> in <paramref name="zone"/>, written the
    /// way <paramref name="culture"/> writes a date and a time, and the
    /// offset from UTC.</summary>
    public static string Said(DateTimeOffset at, TimeZoneInfo zone, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(culture);

        DateTimeOffset local = TimeZoneInfo.ConvertTime(at, zone);
        string offset = local.Offset == TimeSpan.Zero
            ? "UTC"
            : "UTC" + local.ToString("zzz", CultureInfo.InvariantCulture);

        return local.ToString("G", culture) + " " + offset;
    }

    /// <summary><paramref name="at"/> in UTC, as every record writes it.</summary>
    public static string Utc(DateTimeOffset at)
    {
        return at.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
    }
}
