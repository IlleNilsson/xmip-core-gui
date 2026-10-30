using System.Runtime.CompilerServices;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// A surface a test composes (ADR-0068, clause 5): the failed Messages it
/// answers are the test's, page by page, or none at all where
/// <see cref="Listing"/> is null — a surface that cannot list them. Every
/// act and every watch begun is counted; a watch says its first view and
/// ends, as a surface that gave up does.
/// </summary>
internal sealed class ScriptedSurface : IOperatorSurface
{
    /// <summary>What a question for the failed Messages at a scope, from a
    /// place, is answered; null where this surface cannot list them.</summary>
    public Func<string, ulong, FailedMessageList>? Listing { get; set; }

    /// <summary>Every place the failed Messages were asked from, in order.</summary>
    public List<ulong> Asked { get; } = [];

    /// <summary>Every act taken, as <c>act journey</c>.</summary>
    public List<string> Acts { get; } = [];

    /// <summary>How many watches were begun.</summary>
    public int Watches { get; private set; }

    /// <summary>The secrets the node keeps; null where this surface reaches
    /// no node, and so lists none.</summary>
    public List<SecretRecord>? Kept { get; set; }

    /// <summary>Every node the secrets were listed at, in order.</summary>
    public List<string> Listings { get; } = [];

    /// <summary>Every secret set, as <c>node name kind value who</c>: what
    /// reached the surface, so a test sees the value crossed and nothing
    /// else held it.</summary>
    public List<string> SecretsSet { get; } = [];

    /// <inheritdoc />
    public string Source => "scripted";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return [];
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return null;
    }

    /// <inheritdoc />
    public FailedMessageList FailedMessages(string scope, ulong from = 0, uint most = 0)
    {
        Asked.Add(from);

        return Listing is { } listing ? listing(scope, from) : FailedMessageList.Unlisted;
    }

    /// <inheritdoc />
    public MessageOperation Act(string scope, string journey, MessageAct act, string who)
    {
        Acts.Add($"{MessageOperation.Word(act)} {journey}");

        return new MessageOperation(scope, journey, act, true, "applied");
    }

    /// <inheritdoc />
    public IReadOnlyList<SecretRecord> Secrets(string node)
    {
        Listings.Add(node);

        return Kept is { } kept
            ? [.. kept]
            : throw new InvalidOperationException($"{Source} reaches no node, so lists no secrets");
    }

    /// <inheritdoc />
    public SecretOperation SetSecret(
        string node, string name, string kind, string value, string who)
    {
        SecretsSet.Add($"{node} {name} {kind} {value} {who}");

        if (Kept is not { } kept)
        {
            return SecretOperation.Declined(node, name, "REFUSED: reaches no node");
        }

        kept.RemoveAll(secret => secret.Name == name);
        kept.Add(new SecretRecord(name, kind, DateTimeOffset.UtcNow, who));

        return new SecretOperation(node, name, true, $"the secret '{name}', a {kind}, is set");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        Watches++;
        await Task.CompletedTask.ConfigureAwait(false);

        yield return SurfaceChange.Initial(Source);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return string.Empty;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return string.Empty;
    }
}
