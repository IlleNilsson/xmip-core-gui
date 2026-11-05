namespace Xmip.Operations.Test;

/// <summary>
/// The sample cluster this repository ships, <c>samples/cluster.xmip</c> — the
/// file the desktop's <c>gui.xmip</c> names — opens as a cluster and
/// validates in the runtime this estate built. The editing itself is
/// <c>Xmip.Surface</c>'s and proved in <c>Xmip.Surface.Test</c>.
/// </summary>
public sealed class SampleClusterTest
{
    [Fact]
    public void TheSampleOpensAsAClusterAndValidatesInTheRuntime()
    {
        string sample = Path.Combine(
            Root(), "module", "core", "operation", "gui", "samples", "cluster.xmip");
        ClusterConfiguration cluster = ClusterConfiguration.Open(sample);

        Assert.True(cluster.Editable, cluster.Refusal);
        Assert.True(cluster.Views.IsCluster);
        Assert.NotEmpty(cluster.Nodes);

        using ConfigurationRuntime runtime = new(Estate.Library());
        ConfigurationVerdict verdict = runtime.Validate(cluster.Path, cluster.Text);
        Assert.True(verdict.Ok, verdict.Said);
    }

    // The estate's root, above this test's build.
    private static string Root()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (Directory.Exists(Path.Combine(at.FullName, "module", "platform", "runtime")))
            {
                return at.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"{AppContext.BaseDirectory} is not inside the Xmip estate");
    }
}
