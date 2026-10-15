using System.Runtime.Versioning;
using System.Security.Cryptography;
using Xmip.Abi.Operate;
using Xmip.Operations;

namespace Xmip.Operations.Test;

/// <summary>
/// The desktop's keeper, Windows CNG (ADR-0034, amendment 2026-10-15), on
/// this machine: a P-256 key made by the software provider under a name of
/// the test's own — never the operator's — and not exportable; its public
/// key and its signature make an operator's request through the runtime's
/// two halves; forgotten, it is gone. The TPM is not asked here: a key it
/// made would outlive a failed run in the machine's own chip.
/// </summary>
public sealed class CngKeeperTest
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void ASoftwareKeyIsMadeNotExportableAndMakesARequest()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string alias = $"xmip-operations-test-{Guid.NewGuid():N}";
        CngKeeper keeper = new(CngProvider.MicrosoftSoftwareKeyStorageProvider);

        try
        {
            Assert.False(keeper.Holds(alias));
            byte[] publicKey = keeper.Make(alias);
            Assert.True(keeper.Holds(alias));
            Assert.Equal("Windows CNG, in software, not exportable", keeper.Where(alias));
            Assert.Equal(publicKey, keeper.PublicKey(alias));

            using (CngKey kept = CngKey.Open(
                alias, CngProvider.MicrosoftSoftwareKeyStorageProvider))
            {
                Assert.Equal(CngExportPolicies.None, kept.ExportPolicy);
            }

            RuntimeCertificates certificates = RuntimeLibrary.Rules.Certificates;
            Assert.True(certificates.Information(
                "alice@example.org", publicKey, out byte[] information, out string no), no);
            Assert.True(
                certificates.Assemble(
                    information, keeper.Sign(alias, information), out string request, out no),
                no);
            Assert.Contains("CERTIFICATE REQUEST", request, StringComparison.Ordinal);
        }
        finally
        {
            keeper.Forget(alias);
        }

        Assert.False(keeper.Holds(alias));
        Assert.Throws<InvalidOperationException>(() => keeper.Sign(alias, [1]));
    }
}
