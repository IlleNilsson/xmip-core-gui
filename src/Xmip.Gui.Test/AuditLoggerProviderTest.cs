using Microsoft.Extensions.Logging;
using Xmip.Gui.Hosting;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// A circuit that dies is logged by the framework at Error with its
/// exception — the browser shows "an unhandled error has occurred" — and
/// the provider every GUI host adds makes that an audit record (ADR-0062
/// clause 4). The framework's own category and words are used here, so the
/// test is the case that happened on 2026-09-25.
/// </summary>
public sealed class AuditLoggerProviderTest : IDisposable
{
    private const string Circuit = "Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost";

    private readonly TestFolder _place = new("gui-audit");

    public void Dispose()
    {
        _place.Dispose();
    }

    // Beneath the test's own folder and not made: the audit makes it when
    // it records, and only then.
    private string Scratch()
    {
        return _place.Inside("audit");
    }

    [Fact]
    public void AnUnhandledCircuitErrorBecomesAFailureRecordWithItsException()
    {
        string directory = Scratch();
        using AuditLoggerProvider provider = new(new ProgramAudit("xmip-gui-web", directory));
        ILogger logger = provider.CreateLogger(Circuit);

        logger.Log(
            LogLevel.Error,
            new EventId(111, "CircuitUnhandledException"),
            "Unhandled exception in circuit 'probe'.",
            new InvalidOperationException("the snapshot is gone"),
            (said, _) => said);

        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("program = \"xmip-gui-web\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"error logged\"", text, StringComparison.Ordinal);
        Assert.Contains("phase = \"Failure\"", text, StringComparison.Ordinal);
        Assert.Contains("message = \"the snapshot is gone\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"category\" = \"{Circuit}\"", text, StringComparison.Ordinal);
        Assert.Contains(
            "\"event\" = \"CircuitUnhandledException\"", text, StringComparison.Ordinal);
        Assert.Contains(
            "\"exception\" = \"System.InvalidOperationException\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatIsNotAnErrorIsALogAndNotARecord()
    {
        string directory = Scratch();
        using AuditLoggerProvider provider = new(new ProgramAudit("xmip-gui-web", directory));
        ILogger logger = provider.CreateLogger(Circuit);

        Assert.False(logger.IsEnabled(LogLevel.Warning));
        logger.Log(LogLevel.Information, default, "Reading a snapshot", null, (said, _) => said);

        Assert.False(Directory.Exists(directory));
    }
}
