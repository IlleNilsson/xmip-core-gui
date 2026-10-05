using Xmip.Abi.Module;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Operations.Configuration;

/// <summary>
/// The commands the desktop can run that a browser cannot: validating the
/// cluster's <c>xmip.toml</c>, saving it — which slices it for each node and
/// ships each slice where it can go (ADR-0031, amendment 2026-10-05) — and
/// planning this desktop's node from its slice through the native runtime:
/// <c>xmip_start_v1</c> reads, validates and publishes the plan, and runs
/// nothing; a node runs in the program that links its technologies.
/// A browser is sandboxed — no native library, no local file to hand it —
/// which is why configuration and node control live in the desktop GUI
/// (ADR-0014, amendment of 2026-09-05). When the board reads the runtime,
/// these go through that same runtime; when it reads a snapshot, the runtime
/// is loaded here alone, from the library the one discovery rule found. Each
/// is an operator's act and is audited with what the runtime answered
/// (ADR-0062): a save, every node's slice and ship, a refused edit, a
/// validate and a plan.
/// </summary>
public sealed class RuntimeCommands(
    IOperatorSurface surface, string libraryPath, ProgramAudit audit, SliceDelivery delivery)
{
    private NativeOperator? _own;

    /// <summary>Where the cluster lives, where its slices go and which node
    /// is configured here.</summary>
    public SliceDelivery Delivery => delivery;

    /// <summary>Validate the text the editor is holding through the native
    /// runtime, saved or not (ADR-0027, amendment 2026-09-05): a cluster's
    /// file node by node, the runtime's answer the only one given.</summary>
    public ConfigurationVerdict Validate(string configurationPath, string configuration)
    {
        return Audited("validate", Runtime().Validate(configurationPath, configuration));
    }

    /// <summary>
    /// Save the cluster's file and slice it for every node it declares,
    /// shipping each slice where it can go. A node's own document is never
    /// saved here, nor a file another editor changed since this one read it;
    /// a save writes the file whole, the runtime then judges what is on disk,
    /// and every node's slice is audited, then its ship.
    /// </summary>
    public ClusterSave Save(ClusterConfiguration cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        if (!cluster.Views.IsCluster)
        {
            // In the slicing's own words: declare a node first.
            _ = NodeSlice.TrySlice(cluster.Text, null, out _, out string refusal);
            return Failed(cluster.Path, refusal);
        }

        try
        {
            if (!cluster.TryWrite(out string stale))
            {
                return Failed(cluster.Path, stale) with { Stale = true };
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(cluster.Path, exception.Message);
        }

        ConfigurationVerdict verdict = Validate(cluster.Path, cluster.Text);
        IReadOnlyList<NodeDelivery> deliveries = delivery.Deliver(cluster);

        foreach (NodeDelivery node in deliveries)
        {
            Delivered(cluster.Path, node);
        }

        string said = $"saved {Path.GetFileName(cluster.Path)}; {verdict.Said}";
        audit.Record(
            "save cluster configuration",
            AuditPhase.Finished,
            verdict.Ok ? AuditSeverity.Information : AuditSeverity.Warning,
            said,
            new Dictionary<string, string>
            {
                ["configuration"] = cluster.Path,
                ["nodes"] = string.Join(
                    ", ", deliveries.Select(node => $"{node.Node} {node.Word}")),
            });

        return new ClusterSave(true, verdict.Ok, said, deliveries);
    }

    /// <summary>Audit an edit the runtime refused, with its sentence.</summary>
    public void Refused(ClusterEdit edit, string configurationPath, string refusal)
    {
        ArgumentNullException.ThrowIfNull(edit);

        audit.Record(
            "edit cluster configuration",
            AuditPhase.Failure,
            AuditSeverity.Error,
            refusal,
            new Dictionary<string, string>
            {
                ["configuration"] = configurationPath,
                ["edit"] = edit.ToJson(),
            });
    }

    /// <summary>
    /// Plan the node configured here from the saved cluster's file: its
    /// slice, written where that node reads it, then read, validated and
    /// planned by the native runtime, which publishes the plan for the board.
    /// Nothing runs: running a node is the program that links its
    /// technologies (ADR-0018, amendment 2026-09-26). Refused, in words, where
    /// <c>xmip.gui.toml</c> names no node or the runtime will not slice it.
    /// </summary>
    public ConfigurationVerdict PlanNode()
    {
        if (delivery.Node is not { Length: > 0 } node)
        {
            return Audited("plan node", new ConfigurationVerdict(
                delivery.ClusterPath, XmipStatus.Invalid, [],
                "no node is configured here: xmip.gui.toml names no Node"));
        }

        if (!File.Exists(delivery.ClusterPath))
        {
            return Audited("plan node", ConfigurationVerdict.NoFile(delivery.ClusterPath));
        }

        NodeDelivery sliced = delivery.Deliver(File.ReadAllText(delivery.ClusterPath), node);
        Delivered(delivery.ClusterPath, sliced);

        return sliced.Outcome == NodeDelivery.Standing.Refused
            ? Audited("plan node", new ConfigurationVerdict(
                delivery.ClusterPath, XmipStatus.Invalid, [], sliced.Said))
            : Audited("plan node", Runtime().Plan(sliced.Path));
    }

    // A save that wrote nothing, audited with why.
    private ClusterSave Failed(string path, string said)
    {
        audit.Record(
            "save cluster configuration",
            AuditPhase.Failure,
            AuditSeverity.Error,
            said,
            new Dictionary<string, string> { ["configuration"] = path });

        return new ClusterSave(false, false, said, []);
    }

    // One node's slice, then its ship: each a record of its own.
    private void Delivered(string path, NodeDelivery node)
    {
        bool sliced = node.Outcome != NodeDelivery.Standing.Refused;
        Dictionary<string, string> properties = new()
        {
            ["configuration"] = path,
            ["node"] = node.Node,
            ["slice"] = node.Path,
        };

        audit.Record(
            "slice",
            sliced ? AuditPhase.Finished : AuditPhase.Failure,
            sliced ? AuditSeverity.Information : AuditSeverity.Error,
            sliced ? $"{node.Node} sliced to {node.Path}" : node.Said,
            properties);

        if (sliced)
        {
            bool shipped = node.Outcome == NodeDelivery.Standing.Shipped;
            audit.Record(
                "ship",
                shipped ? AuditPhase.Finished : AuditPhase.Failure,
                shipped ? AuditSeverity.Information : AuditSeverity.Warning,
                node.Said,
                properties);
        }
    }

    // The act and what came of it, one record: Finished when the runtime did
    // what was asked, Failure with its sentence when it did not.
    private ConfigurationVerdict Audited(string action, ConfigurationVerdict verdict)
    {
        audit.Record(
            action,
            verdict.Ok ? AuditPhase.Finished : AuditPhase.Failure,
            verdict.Ok ? AuditSeverity.Information : AuditSeverity.Error,
            verdict.Said,
            new Dictionary<string, string>
            {
                ["configuration"] = verdict.Path,
                ["status"] = $"{verdict.Status}",
                ["problems"] = string.Join("; ", verdict.Problems),
            });

        return verdict;
    }

    private NativeOperator Runtime()
    {
        return surface as NativeOperator ?? (_own ??= new NativeOperator(libraryPath));
    }
}
