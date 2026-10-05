namespace Xmip.Operations.Configuration;

/// <summary>
/// What came of saving the cluster's <c>xmip.toml</c>: whether it was
/// written, whether the runtime found it good, the sentence, and how each
/// node's slice stands — sliced and shipped, sliced and not shipped, or
/// refused in the runtime's words (ADR-0031, amendment 2026-10-05).
/// </summary>
/// <param name="Saved">Whether the file was written.</param>
/// <param name="Valid">Whether the runtime validated what was written.</param>
/// <param name="Said">The sentence for a person.</param>
/// <param name="Nodes">Each node's slice, in the order the file declares
/// them; empty when nothing was written.</param>
public sealed record ClusterSave(
    bool Saved, bool Valid, string Said, IReadOnlyList<NodeDelivery> Nodes);
