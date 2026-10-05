namespace Xmip.Operations.Configuration;

/// <summary>
/// What became of one node's slice when the cluster's <c>xmip.toml</c> was
/// saved (ADR-0031, amendment 2026-10-05: <i>the cluster TOML file is
/// sliced into node TOML files and shipped to each node on save</i>): the
/// node, how it stands, the file its slice was written to, and the sentence
/// the page shows.
/// </summary>
/// <param name="Node">The node's key under <c>[nodes]</c>.</param>
/// <param name="Outcome">How it stands.</param>
/// <param name="Path">The file its slice was written to; empty when it was
/// refused.</param>
/// <param name="Said">The sentence for a person: the runtime's refusal
/// where it refused.</param>
public sealed record NodeDelivery(
    string Node, NodeDelivery.Standing Outcome, string Path, string Said)
{
    /// <summary>How a node's slice stands after a save.</summary>
    public enum Standing
    {
        /// <summary>Sliced and placed where the node reads it.</summary>
        Shipped,

        /// <summary>Sliced and written, and not put on the node: no Xmip
        /// path reaches it from here.</summary>
        NotShipped,

        /// <summary>The runtime would not slice it; nothing was written.</summary>
        Refused,
    }

    /// <summary>The word the page and the audit say it by.</summary>
    public string Word => Outcome switch
    {
        Standing.Shipped => "shipped",
        Standing.NotShipped => "sliced, not shipped",
        _ => "refused",
    };
}
