using System.Runtime.Versioning;
using System.Security.Cryptography;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The operator's key kept by Windows CNG (ADR-0034, amendment 2026-10-15):
/// made in the TPM through the Microsoft Platform Crypto Provider where the
/// machine has one that makes P-256 keys, else by the Microsoft Software Key
/// Storage Provider, never exportable either way. The key is the user's,
/// persisted by name, and every signature is CNG's; this process never holds
/// its private half.
/// </summary>
/// <remarks>
/// CNG: <c>CngKey.Create</c> with <c>CngKeyCreationParameters.Provider</c>
/// and <c>ExportPolicy = None</c>; the providers and what each makes:
/// https://learn.microsoft.com/windows/win32/seccertenroll/cng-key-storage-providers.
/// A TPM signature is the TPM's time, tens of milliseconds, which is the
/// hardware's load.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class CngKeeper : IOperatorKeeper
{
    private readonly CngProvider[] _providers;

    /// <summary>The TPM first, then software.</summary>
    public CngKeeper()
        : this(CngProvider.MicrosoftPlatformCryptoProvider,
               CngProvider.MicrosoftSoftwareKeyStorageProvider)
    {
    }

    /// <summary>The providers a key is made by, tried in order, and looked
    /// for in.</summary>
    public CngKeeper(params CngProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.Length > 0
            ? providers
            : throw new ArgumentException("a keeper names a provider", nameof(providers));
    }

    /// <inheritdoc />
    public string Name => Where(OperatorCertificate.KeptAlias) ?? "Windows CNG";

    /// <summary>Where the key under <paramref name="alias"/> is kept: the
    /// TPM, or software; null where none is kept.</summary>
    public string? Where(string alias)
    {
        return Holder(alias) is not { } provider
            ? null
            : provider == CngProvider.MicrosoftPlatformCryptoProvider
                ? "the TPM, through Windows CNG"
                : "Windows CNG, in software, not exportable";
    }

    /// <inheritdoc />
    public bool Holds(string alias)
    {
        return Holder(alias) is not null;
    }

    /// <inheritdoc />
    public byte[] Make(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        List<string> refused = [];

        foreach (CngProvider provider in _providers)
        {
            try
            {
                using CngKey key = CngKey.Create(
                    CngAlgorithm.ECDsaP256, alias, new CngKeyCreationParameters
                    {
                        Provider = provider,
                        ExportPolicy = CngExportPolicies.None,
                        KeyUsage = CngKeyUsages.Signing,
                    });
                using ECDsaCng held = new(key);
                return held.ExportSubjectPublicKeyInfo();
            }
            catch (CryptographicException failed)
            {
                refused.Add($"{provider.Provider}: {failed.Message.Trim()}");
            }
        }

        throw new InvalidOperationException(
            $"Windows CNG made no P-256 key: {string.Join("; ", refused)}");
    }

    /// <inheritdoc />
    public byte[] PublicKey(string alias)
    {
        using CngKey key = Open(alias);
        using ECDsaCng held = new(key);
        return held.ExportSubjectPublicKeyInfo();
    }

    /// <inheritdoc />
    public byte[] Sign(string alias, byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            using CngKey key = Open(alias);
            using ECDsaCng held = new(key);
            return held.SignData(
                message, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException failed)
        {
            throw new InvalidOperationException(
                $"Windows CNG did not sign: {failed.Message.Trim()}", failed);
        }
    }

    /// <inheritdoc />
    public void Forget(string alias)
    {
        if (Holder(alias) is not null)
        {
            Open(alias).Delete();
        }
    }

    private CngKey Open(string alias)
    {
        return Holder(alias) is { } provider
            ? CngKey.Open(alias, provider)
            : throw new InvalidOperationException($"Windows CNG keeps no key named {alias}");
    }

    // The provider that keeps a key of that name; a provider this machine
    // does not have keeps none.
    private CngProvider? Holder(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        foreach (CngProvider provider in _providers)
        {
            try
            {
                if (CngKey.Exists(alias, provider))
                {
                    return provider;
                }
            }
            catch (CryptographicException)
            {
                // No such provider here: it keeps nothing.
            }
        }

        return null;
    }
}
