using System.Runtime.InteropServices;
using System.Text;

namespace Xmip.Gui.Web.Surface;

/// <summary>
/// The operator boundary as <c>include/xmip_operate.h</c> declares it, crossed
/// by P/Invoke. ADR-0012 clause 1 applies here too: the header is normative
/// and this is not; where they differ, the header is right and this is a
/// defect.
/// </summary>
public sealed unsafe class NativeOperator : IOperatorSurface, IDisposable
{
    private const uint Version = 1u;
    private const string Entrypoint = "xmip_operate_v1";

    private readonly nint _library;
    private readonly Operate _table;
    private bool _disposed;

    /// <inheritdoc />
    public string Source { get; }

    private NativeOperator(nint library, Operate table, string path)
    {
        _library = library;
        _table = table;
        Source = path;
    }

    /// <summary>
    /// Load the runtime's native library and take its operator table.
    /// Returns <c>null</c>, with the reason, when it cannot — a surface that
    /// cannot reach a node shows that rather than an empty tree.
    /// </summary>
    public static NativeOperator? Load(string path, out string reason)
    {
        if (!NativeLibrary.TryLoad(path, out nint library))
        {
            reason = $"no runtime library at {path}";
            return null;
        }

        if (!NativeLibrary.TryGetExport(library, Entrypoint, out nint symbol))
        {
            NativeLibrary.Free(library);
            reason = $"{path} does not export {Entrypoint}";
            return null;
        }

        delegate* unmanaged[Cdecl]<uint, Operate*, int> entry =
            (delegate* unmanaged[Cdecl]<uint, Operate*, int>)symbol;
        Operate table;
        int status = entry(Version, &table);

        if (status != 0)
        {
            NativeLibrary.Free(library);
            reason = $"{Entrypoint} refused version {Version} with status {status}";
            return null;
        }

        reason = string.Empty;
        return new NativeOperator(library, table, path);
    }

    /// <summary>
    /// Start a node from its configuration file — as far as the runtime can
    /// today, which is read, build, validate and plan. Returns what the
    /// runtime said. The table's next read shows the result either way.
    /// </summary>
    public string Start(string configurationPath)
    {
        if (!NativeLibrary.TryGetExport(_library, "xmip_start_v1", out nint symbol))
        {
            return "this runtime does not export xmip_start_v1";
        }

        delegate* unmanaged[Cdecl]<XmipStr, int> start =
            (delegate* unmanaged[Cdecl]<XmipStr, int>)symbol;
        byte[] bytes = Encoding.UTF8.GetBytes(configurationPath);

        fixed (byte* text = bytes)
        {
            int status = start(new XmipStr(text, (nuint)bytes.Length));

            return status == 0
                ? $"started {configurationPath}"
                : $"{configurationPath} refused with status {status}; the health tree says why";
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(scope);
        List<HealthRecord> found = [];

        fixed (byte* text = bytes)
        {
            XmipStr xmipScope = new(text, (nuint)bytes.Length);
            nuint needed = 0;

            // Ask for the count first, then for exactly that many. The header
            // promises the true count in out_len however small the buffer.
            int probe = _table.Health(_table.Ctx, xmipScope, null, 0, &needed);

            if (probe != 0 || needed == 0)
            {
                return found;
            }

            HealthEntry[] entries = new HealthEntry[needed];

            fixed (HealthEntry* buffer = entries)
            {
                _ = _table.Health(_table.Ctx, xmipScope, buffer, needed, &needed);
            }

            foreach (HealthEntry entry in entries)
            {
                found.Add(new HealthRecord(
                    entry.Scope.Read(),
                    (HealthState)entry.Health,
                    entry.Evidence.Read(),
                    FromNanos(entry.ObservedUnixNanos)));
            }
        }

        return found;
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(scope);

        fixed (byte* text = bytes)
        {
            XmipStr xmipScope = new(text, (nuint)bytes.Length);
            Measurement entry;
            nuint len = 0;

            int status = _table.Measure(_table.Ctx, xmipScope, (int)counted, &entry, 1, &len);

            return status != 0 || len == 0
                ? null
                : new MeasurementRecord(
                    entry.Scope.Read(),
                    (Counted)entry.Counted,
                    entry.Value,
                    FromNanos(entry.WindowStartUnixNanos),
                    FromNanos(entry.WindowEndUnixNanos),
                    FromNanos(entry.ObservedUnixNanos));
        }
    }

    private static DateTimeOffset FromNanos(long nanos)
    {
        return DateTimeOffset.UnixEpoch.AddTicks(nanos / 100);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_table.Destroy is not null)
        {
            _table.Destroy(_table.Ctx);
        }

        NativeLibrary.Free(_library);
    }

    // -- The header's shapes. Section numbers refer to xmip_operate.h. -----

    /// <summary>Section 2 of xmip_module.h: a borrowed UTF-8 string.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct XmipStr(byte* ptr, nuint len)
    {
        public readonly byte* Ptr = ptr;
        public readonly nuint Len = len;

        public string Read()
        {
            return Ptr is null || Len == 0
                ? string.Empty
                : Encoding.UTF8.GetString(Ptr, checked((int)Len));
        }
    }

    /// <summary>Section 3.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct HealthEntry
    {
        public XmipStr Scope;
        public int Health;
        public XmipStr Evidence;
        public long ObservedUnixNanos;
    }

    /// <summary>Section 4.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Measurement
    {
        public XmipStr Scope;
        public int Counted;
        public ulong Value;
        public long WindowStartUnixNanos;
        public long WindowEndUnixNanos;
        public long ObservedUnixNanos;
    }

    /// <summary>Section 5. The table the runtime fills.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Operate
    {
        public uint AbiVersion;
        public void* Ctx;
        public delegate* unmanaged[Cdecl]<void*, XmipStr, HealthEntry*, nuint, nuint*, int> Health;
        public delegate* unmanaged[Cdecl]<void*, XmipStr, int, Measurement*, nuint, nuint*, int> Measure;
        public delegate* unmanaged[Cdecl]<void*, void> Destroy;
    }
}
