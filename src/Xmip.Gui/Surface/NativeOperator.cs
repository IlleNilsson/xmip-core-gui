using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Gui.Surface;

/// <summary>
/// The screens' view of a loaded runtime: <see cref="IOperatorSurface"/> over
/// <see cref="Operator"/>, the binding in xmip-core-abi. Nothing here crosses
/// the C ABI itself — the binding does that once, for every surface (ADR-0014,
/// amendment of 2026-08-26) — and what is left is turning a status into the
/// words an operator reads on screen.
/// </summary>
public sealed class NativeOperator : IOperatorSurface, IDisposable
{
    private readonly Operator _runtime;

    private NativeOperator(Operator runtime)
    {
        _runtime = runtime;
    }

    /// <inheritdoc />
    public string Source => _runtime.Source;

    /// <summary>
    /// Load the runtime's native library and take its operator table.
    /// Returns <c>null</c>, with the reason, when it cannot — a surface that
    /// cannot reach a node shows that rather than an empty tree.
    /// </summary>
    public static NativeOperator? Load(string path, out string reason)
    {
        Operator? runtime = Operator.Load(path, out reason);

        return runtime is null ? null : new NativeOperator(runtime);
    }

    /// <summary>
    /// Start a node from its configuration file — as far as the runtime can
    /// today, which is read, build, validate and plan. Returns what the
    /// runtime said. The table's next read shows the result either way.
    /// </summary>
    public string Start(string configurationPath)
    {
        XmipStatus status = _runtime.Start(configurationPath);

        return status switch
        {
            XmipStatus.Ok => $"started {configurationPath}",
            XmipStatus.Unsupported =>
                $"this runtime does not export {OperateAbi.StartEntrypoint}",
            _ => $"{configurationPath} refused: {status.Explain()}; the health tree says why",
        };
    }

    /// <summary>
    /// Validate a node's configuration file without starting it. The file's
    /// text crosses, not its path — the runtime checks a proposed document and
    /// publishes nothing (ADR-0027 clause 9), so the answer carries the
    /// problems itself. A command a browser cannot run: it reads a local file
    /// and loads the native runtime.
    /// </summary>
    public string Validate(string configurationPath)
    {
        if (!File.Exists(configurationPath))
        {
            return $"no configuration at {configurationPath}";
        }

        ValidationRecord answer = _runtime.Validate(File.ReadAllText(configurationPath));

        if (answer.IsValid)
        {
            return $"{configurationPath} is valid";
        }

        if (answer.Status == XmipStatus.Unsupported)
        {
            return $"this runtime does not export {OperateAbi.ValidateEntrypoint}";
        }

        string why = answer.Problems.Count == 0
            ? answer.Status.Explain()
            : string.Join("; ", answer.Problems);

        return $"{configurationPath} is invalid: {why}";
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return _runtime.Health(scope);
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return _runtime.Measure(scope, counted);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        XmipStatus status = _runtime.PauseScope(scope, who);

        return status == XmipStatus.Ok
            ? $"paused {scope}"
            : $"nothing to pause at {scope} ({status.Explain()})";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        XmipStatus status = _runtime.ResumeScope(scope);

        return status == XmipStatus.Ok
            ? $"resumed {scope}"
            : $"nothing to resume at {scope} ({status.Explain()})";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _runtime.Dispose();
    }
}
