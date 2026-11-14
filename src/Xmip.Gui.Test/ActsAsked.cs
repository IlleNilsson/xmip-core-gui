using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The acts a view asked of a node, as the view recorded them in its host's
/// audit — <c>&lt;noun&gt;.&lt;act&gt;</c> with the node and the one acted on
/// — read back oldest first by the audit capability's one reader, for a test
/// of the views that ask them: which act, and on which one. A snapshot is
/// never acted on (ADR-0014, amendment 2026-11-13), so what a view asked is
/// what it recorded.
/// </summary>
internal static class ActsAsked
{
    /// <summary>Each act the view recorded in <paramref name="audit"/> on
    /// <paramref name="node"/>, oldest first, as <c>action one</c>, the one
    /// acted on being the record's <paramref name="target"/>
    /// property.</summary>
    public static string[] Of(string audit, string node, string target)
    {
        if (!File.Exists(Path.Combine(audit, "audit.toml")))
        {
            return [];
        }

        AuditRead read = new ProgramAudit("Xmip.Gui.Test", audit)
            .Read(new AuditQuery { Order = "ascending", IncludeHidden = true });

        return
        [
            .. read.Records
                .Where(record => record.Properties.TryGetValue("node", out string? at)
                    && at.EndsWith($"/node/{node}", StringComparison.Ordinal)
                    && record.Properties.ContainsKey(target))
                .Select(record => $"{record.Action} {record.Properties[target]}"),
        ];
    }
}
