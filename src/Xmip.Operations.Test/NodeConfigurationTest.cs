using Xmip.Operations.Configuration;
using Xmip.Surface;

namespace Xmip.Operations.Test;

/// <summary>
/// The editor holds a view over the one configuration document and the
/// runtime judges it (open problem 25, row b). Every verdict here is the
/// runtime's own, through <c>xmip_validate_v1</c>: the library is found by
/// the one rule (<see cref="RuntimeLibrary"/>), <c>XMIP_RUNTIME_LIBRARY</c>
/// first, else the runtime's debug build in the estate this repository
/// builds inside (ADR-0014, amendment 2026-09-09).
/// </summary>
public sealed class NodeConfigurationTest : IDisposable
{
    private const string Head = """
        [service]
        name = "xmip-edge-01"
        cluster_name = "lab"
        node_name = "edge-01"

        """;

    private readonly NativeOperator _runtime = new(Library());
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-node-configuration-").FullName;

    [Fact]
    public void ADocumentTheEditorSavesValidatesInTheRuntime()
    {
        string sample = Path.Combine(
            Estate(), "module", "core", "operation", "gui", "samples", "edge-01.xmip.toml");
        NodeConfiguration node = NodeConfiguration.Read(sample);

        node.Processes[0].ExecutionStyle = "concurrent";
        node.SendLocations.Add(new NodeConfiguration.Location
        {
            Name = "archive-out",
            Start = false,
            Transport = "file",
            Address = "C:/xmip/edge-01/out/archive",
        });

        string saved = Path.Combine(_directory, "edge-01.xmip.toml");
        node.Write(saved);

        ConfigurationVerdict verdict = _runtime.Validate(saved);
        Assert.True(verdict.Ok, verdict.Said);

        // What the editor does not show is written back as it was read.
        NodeConfiguration again = NodeConfiguration.Read(saved);
        Assert.Equal("concurrent", again.Processes[0].ExecutionStyle);
        Assert.Equal(2, again.SendLocations.Count);
        Assert.Contains("xmip_core_transport_file", File.ReadAllText(saved), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("start", "name = \"in\"\ntransport = \"file\"\naddress = \"C:/in\"\n")]
    [InlineData("transport", "name = \"in\"\nstart = true\naddress = \"C:/in\"\n")]
    public void ALocationMissingStartOrTransportIsRefusedNotDefaulted(
        string missing, string row)
    {
        NodeConfiguration node = NodeConfiguration.Parse($"{Head}[[receive_locations]]\n{row}");
        NodeConfiguration.Location location = node.ReceiveLocations[0];

        if (missing == "start")
        {
            Assert.Null(location.Start);
        }
        else
        {
            Assert.Null(location.Transport);
        }

        string written = node.ToToml();
        Assert.DoesNotContain($"{missing} =", written, StringComparison.Ordinal);

        ConfigurationVerdict verdict = _runtime.Validate("in-memory.xmip.toml", written);
        Assert.False(verdict.Ok);
        Assert.Contains(verdict.Problems, problem => problem.Contains(missing, StringComparison.Ordinal));
    }

    [Fact]
    public void AnEscapedStringRoundTripsThroughTheEditorAndTheRuntime()
    {
        const string Name = "say \"hi\"";
        const string Address = "C:\\in\\t\u00e9st\nnext";

        NodeConfiguration read = NodeConfiguration.Parse(
            Head + "[[receive_locations]]\nname = \"say \\\"hi\\\"\"\nstart = true\n"
                + "transport = \"file\"\naddress = \"C:\\\\in\\\\t\\u00e9st\\nnext\"\n");
        Assert.Equal(Name, read.ReceiveLocations[0].Name);
        Assert.Equal(Address, read.ReceiveLocations[0].Address);

        string written = read.ToToml();
        NodeConfiguration again = NodeConfiguration.Parse(written);
        Assert.Equal(Name, again.ReceiveLocations[0].Name);
        Assert.Equal(Address, again.ReceiveLocations[0].Address);

        ConfigurationVerdict verdict = _runtime.Validate("in-memory.xmip.toml", written);
        Assert.True(verdict.Ok, verdict.Said);
    }

    [Fact]
    public void AKeyIsMatchedWholeAndOneTheEditorDoesNotKnowIsKept()
    {
        NodeConfiguration node = NodeConfiguration.Parse(Head + """
            [[xmip_processes]]
            name_note = "not the name"
            started = true
            required_modules = []
            xmip_subprocesses = []
            extensions = []
            """);
        NodeConfiguration.XmipProcess process = node.Processes[0];

        Assert.Equal("", process.Name);
        Assert.Null(process.Start);

        string all = node.ToToml();
        string written = all[all.IndexOf("[[xmip_processes]]", StringComparison.Ordinal)..];
        Assert.Contains("name_note = \"not the name\"", written, StringComparison.Ordinal);
        Assert.Contains("started = true", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\nname =", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\nstart =", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AProcessTheEditorAddsNeedsNoListsToValidate()
    {
        // ADR-0031, amendment 2026-09-24: the three lists default to empty.
        NodeConfiguration node = NodeConfiguration.Parse(Head);
        node.Processes.Add(new NodeConfiguration.XmipProcess { Name = "minimal", Start = true });

        string written = node.ToToml();
        Assert.DoesNotContain("required_modules", written, StringComparison.Ordinal);

        ConfigurationVerdict verdict = _runtime.Validate("in-memory.xmip.toml", written);
        Assert.True(verdict.Ok, verdict.Said);
    }

    [Fact]
    public void CommentsAndLayoutSurviveAnEditAndASave()
    {
        const string Original = """
            # The edge node in the lab. Keep this line.
            [service]
            name = "xmip-edge-01"
            cluster_name = "lab" # the lab cluster
            node_name = "edge-01"

            # The one Process.
            [[xmip_processes]]
            # Inside the Process block.
            name = "approval"
            start = true # starts with the node

            [[receive_locations]]
            name = "orders-in" # where orders arrive
            start = true
            transport = "file"
            address = "C:/in"

            # Trailing comment at the end.

            """;

        NodeConfiguration node = NodeConfiguration.Parse(Original);
        node.Processes[0].Name = "approval-2";
        node.Processes[0].Start = false;
        node.ReceiveLocations[0].Name = "orders";
        node.SendLocations.Add(new NodeConfiguration.Location
        {
            Name = "out",
            Start = true,
            Transport = "file",
            Address = "C:/out",
        });

        string saved = Path.Combine(_directory, "commented.xmip.toml");
        node.Write(saved);
        string written = File.ReadAllText(saved);

        string expected = Original
            .Replace("name = \"approval\"", "name = \"approval-2\"", StringComparison.Ordinal)
            .Replace("start = true # starts", "start = false # starts", StringComparison.Ordinal)
            .Replace("name = \"orders-in\"", "name = \"orders\"", StringComparison.Ordinal)
            .Replace(
                "address = \"C:/in\"\n",
                """
                address = "C:/in"

                [[send_locations]]
                name = "out"
                start = true
                transport = "file"
                address = "C:/out"

                """,
                StringComparison.Ordinal);
        Assert.Equal(expected, written);

        ConfigurationVerdict verdict = _runtime.Validate(saved);
        Assert.True(verdict.Ok, verdict.Said);
    }

    public void Dispose()
    {
        _runtime.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private static string Library()
    {
        string library = RuntimeLibrary.Choose(
            null,
            Environment.GetEnvironmentVariable(RuntimeLibrary.EnvironmentVariable),
            Estate(),
            Path.Combine(Estate(), "module", "platform", "runtime", "target", "debug"));

        Assert.True(
            File.Exists(library),
            $"no runtime at {library}: run cargo build in module/platform/runtime, "
                + $"or set {RuntimeLibrary.EnvironmentVariable}");

        return library;
    }

    private static string Estate()
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
