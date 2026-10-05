using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// What the desktop's configuration tests share: the estate this repository
/// builds inside, the runtime it built — found by the one rule
/// (<see cref="RuntimeLibrary"/>), <c>XMIP_RUNTIME_LIBRARY</c> first, else the
/// library the binding's project copies beside every test assembly — and a
/// cluster's <c>xmip.toml</c> written from the test cluster's names.
/// </summary>
internal static class Estate
{
    /// <summary>The test cluster, whose names every test takes.</summary>
    public static readonly TestCluster Cluster = TestCluster.Read();

    /// <summary>The Receive Location the written cluster shares.</summary>
    public const string Location = "drop";

    /// <summary>A comment the written cluster holds, which every edit keeps.</summary>
    public const string Comment = "# The cluster, as the desktop is given it.";

    /// <summary>A cluster's file declaring the test cluster's first two
    /// nodes, written into <paramref name="directory"/> and opened.</summary>
    public static ClusterConfiguration Written(string directory)
    {
        string path = Path.Combine(directory, "xmip.toml");
        File.WriteAllText(path, $"""
            {Comment}
            [service]
            name = "xmip"
            cluster_name = "{Cluster.Name}"

            [[receive_locations]]
            name = "{Location}"
            start = true
            transport = "file"
            address = "C:/xmip/in"

            [nodes.{Cluster.Nodes[0]}]

            [nodes.{Cluster.Nodes[1]}]

            """);

        return ClusterConfiguration.Open(path);
    }

    /// <summary>The runtime this estate built.</summary>
    public static string Library()
    {
        string library = RuntimeLibrary.Choose(
            null,
            Environment.GetEnvironmentVariable(RuntimeLibrary.EnvironmentVariable),
            RuntimeLibrary.Beside,
            RuntimeLibrary.Beside);

        Assert.True(
            File.Exists(library),
            $"no runtime at {library}: run cargo build in module/platform/runtime, then build "
                + $"this project, or set {RuntimeLibrary.EnvironmentVariable}");

        return library;
    }

    /// <summary>The estate's root, above this test's build.</summary>
    public static string Root()
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
