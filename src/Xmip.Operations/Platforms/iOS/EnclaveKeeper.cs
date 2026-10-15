using Foundation;
using Security;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The operator's key kept by the iOS keychain in the Secure Enclave
/// (ADR-0034, amendment 2026-10-15): a P-256 key made inside the enclave,
/// which keeps it and signs with it and hands out only its public key and
/// its signatures, reachable on this phone alone and only while it is
/// unlocked. Written and not built: iOS is built only on macOS.
/// </summary>
/// <remarks>
/// Apple, "Protecting keys with the Secure Enclave":
/// https://developer.apple.com/documentation/security/protecting-keys-with-the-secure-enclave
/// — <c>kSecAttrKeyTypeECSECPrimeRandom</c>, 256 bits,
/// <c>kSecAttrTokenIDSecureEnclave</c>, the private key permanent and
/// tagged, its access <c>kSecAttrAccessibleWhenUnlockedThisDeviceOnly</c>
/// with <c>privateKeyUsage</c>; the public key by <c>SecKeyCopyPublicKey</c>
/// and its X9.63 external representation, the uncompressed point, which the
/// runtime reads as it is; signatures by <c>SecKeyCreateSignature</c> with
/// <c>ecdsaSignatureMessageX962SHA256</c>, DER. The simulator has no Secure
/// Enclave, so there the key cannot be made, and the keeper says so.
/// </remarks>
public sealed class EnclaveKeeper : IOperatorKeeper
{
    /// <inheritdoc />
    public string Name => "the iOS keychain, in the Secure Enclave";

    /// <inheritdoc />
    public bool Holds(string alias)
    {
        return Find(alias) is not null;
    }

    /// <inheritdoc />
    public byte[] Make(string alias)
    {
        using SecAccessControl access = new(
            SecAccessible.WhenUnlockedThisDeviceOnly, SecAccessControlCreateFlags.PrivateKeyUsage);
        SecKeyGenerationParameters parameters = new()
        {
            KeyType = SecKeyType.ECSecPrimeRandom,
            KeySizeInBits = 256,
            TokenID = SecTokenID.SecureEnclave,
            PrivateKeyAttrs = new SecKeyParameters
            {
                IsPermanent = true,
                ApplicationTag = Tag(alias),
                AccessControl = access,
            },
        };

        using SecKey key = SecKey.CreateRandomKey(parameters, out NSError? error)
            ?? throw new InvalidOperationException(
                $"the Secure Enclave made no key: {error?.LocalizedDescription}");
        return Point(key);
    }

    /// <inheritdoc />
    public byte[] PublicKey(string alias)
    {
        using SecKey key = Find(alias)
            ?? throw new InvalidOperationException($"the keychain keeps no key named {alias}");
        return Point(key);
    }

    /// <inheritdoc />
    public byte[] Sign(string alias, byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);

        using SecKey key = Find(alias)
            ?? throw new InvalidOperationException($"the keychain keeps no key named {alias}");
        using NSData signed = NSData.FromArray(message);
        using NSData signature = key.CreateSignature(
                SecKeyAlgorithm.EcdsaSignatureMessageX962Sha256, signed, out NSError? error)
            ?? throw new InvalidOperationException(
                $"the Secure Enclave did not sign: {error?.LocalizedDescription}");
        return signature.ToArray();
    }

    /// <inheritdoc />
    public void Forget(string alias)
    {
        _ = SecKeyChain.Remove(Query(alias));
    }

    // The uncompressed point the public half's external representation is.
    private static byte[] Point(SecKey key)
    {
        using SecKey open = key.GetPublicKey()
            ?? throw new InvalidOperationException("the key has no public half");
        using NSData point = open.GetExternalRepresentation(out NSError? error)
            ?? throw new InvalidOperationException(
                $"the keychain gave no public key: {error?.LocalizedDescription}");
        return point.ToArray();
    }

    private static SecKey? Find(string alias)
    {
        return SecKeyChain.QueryAsConcreteType(Query(alias), out SecStatusCode _) as SecKey;
    }

    private static SecRecord Query(string alias)
    {
        return new SecRecord(SecKind.Key)
        {
            ApplicationTag = Tag(alias),
            KeyType = SecKeyType.ECSecPrimeRandom,
            KeyClass = SecKeyClass.Private,
        };
    }

    private static NSData Tag(string alias)
    {
        return NSData.FromString($"se.consid.xmip.operations.{alias}", NSStringEncoding.UTF8);
    }
}
