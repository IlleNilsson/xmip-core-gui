using Android.Content.PM;
using Signature = Java.Security.Signature;
using Android.Security.Keystore;
using Java.Security;
using Java.Security.Spec;
using Xmip.Surface;

namespace Xmip.Operations;

/// <summary>
/// The operator's key kept by the Android Keystore (ADR-0034, amendment
/// 2026-10-15): an EC key on P-256 (secp256r1) made inside the
/// <c>AndroidKeyStore</c> provider for signing with SHA-256 alone, in
/// StrongBox where the phone has one, and never exportable — the keystore
/// hands out its public key and its signatures, never its private half.
/// </summary>
/// <remarks>
/// The Android Keystore system:
/// https://developer.android.com/privacy-and-security/keystore. StrongBox
/// (Android 9, API level 28) takes P-256 ECDSA; a phone that declares no
/// <c>FEATURE_STRONGBOX_KEYSTORE</c> is not asked, and one that cannot make
/// the key there throws <c>StrongBoxUnavailableException</c>, after which
/// the key is made by the phone's trusted environment instead, as the
/// documentation says to. Keystore calls may take time, so the page calls
/// this away from the main thread.
/// </remarks>
public sealed class AndroidKeeper : IOperatorKeeper
{
    private const string Store = "AndroidKeyStore";

    /// <inheritdoc />
    public string Name => HasStrongBox()
        ? "the Android Keystore, in StrongBox"
        : "the Android Keystore";

    /// <inheritdoc />
    public bool Holds(string alias)
    {
        return Opened().ContainsAlias(alias);
    }

    /// <inheritdoc />
    public byte[] Make(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        try
        {
            try
            {
                Generate(alias, strongBox: HasStrongBox());
            }
            catch (StrongBoxUnavailableException)
            {
                Generate(alias, strongBox: false);
            }

            return PublicKey(alias);
        }
        catch (Java.Lang.Exception failed)
        {
            throw new InvalidOperationException(
                $"the Android Keystore made no key: {failed.Message}", failed);
        }
    }

    /// <inheritdoc />
    public byte[] PublicKey(string alias)
    {
        // X.509's SubjectPublicKeyInfo, as Java encodes a public key.
        return Opened().GetCertificate(alias)?.PublicKey?.GetEncoded()
            ?? throw new InvalidOperationException(
                $"the Android Keystore keeps no key named {alias}");
    }

    /// <inheritdoc />
    public byte[] Sign(string alias, byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            if (Opened().GetEntry(alias, null) is not KeyStore.PrivateKeyEntry entry)
            {
                throw new InvalidOperationException(
                    $"the Android Keystore keeps no key named {alias}");
            }

            // DER r and s, as X.509 carries an ECDSA signature.
            Signature signer = Signature.GetInstance("SHA256withECDSA")
                ?? throw new InvalidOperationException("no SHA256withECDSA on this phone");
            signer.InitSign(entry.PrivateKey);
            signer.Update(message);
            return signer.Sign() ?? throw new InvalidOperationException("no signature");
        }
        catch (Java.Lang.Exception failed)
        {
            throw new InvalidOperationException(
                $"the Android Keystore did not sign: {failed.Message}", failed);
        }
    }

    /// <inheritdoc />
    public void Forget(string alias)
    {
        KeyStore store = Opened();

        if (store.ContainsAlias(alias))
        {
            store.DeleteEntry(alias);
        }
    }

    private static void Generate(string alias, bool strongBox)
    {
        KeyGenParameterSpec.Builder builder = new(
            alias, KeyStorePurpose.Sign | KeyStorePurpose.Verify);
        builder.SetAlgorithmParameterSpec(new ECGenParameterSpec("secp256r1"));
        builder.SetDigests(KeyProperties.DigestSha256);

        if (strongBox && OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            builder.SetIsStrongBoxBacked(true);
        }

        KeyPairGenerator generator =
            KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, Store)
            ?? throw new InvalidOperationException("no EC key generator in the Android Keystore");
        generator.Initialize(builder.Build());
        _ = generator.GenerateKeyPair();
    }

    private static bool HasStrongBox()
    {
        return OperatingSystem.IsAndroidVersionAtLeast(28)
            && Android.App.Application.Context.PackageManager?.HasSystemFeature(
                PackageManager.FeatureStrongboxKeystore) == true;
    }

    private static KeyStore Opened()
    {
        KeyStore store = KeyStore.GetInstance(Store)
            ?? throw new InvalidOperationException("no Android Keystore on this phone");
        store.Load(null);
        return store;
    }
}
