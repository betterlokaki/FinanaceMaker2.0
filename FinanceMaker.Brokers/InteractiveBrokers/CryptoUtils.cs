using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;

namespace FinanceMaker.Brokers.InteractiveBrokers;

internal static class CryptoUtils
{
    public static string DecryptRsaPkcs1ToHex(string privateKeyPemPath, string base64CipherText)
    {
        using var reader = File.OpenText(privateKeyPemPath);
        var pemObj = new PemReader(reader).ReadObject();
        var key = pemObj as RsaPrivateCrtKeyParameters ?? throw new ArgumentException("Invalid RSA private key");

        // Wrap raw RSA engine with PKCS1 v1.5 padding
        var engine = new Pkcs1Encoding(new RsaEngine());
        engine.Init(false, key); // false = decryption

        var cipherBytes = Convert.FromBase64String(base64CipherText);
        var plain = engine.ProcessBlock(cipherBytes, 0, cipherBytes.Length);

        return BitConverter.ToString(plain).Replace("-", "").ToLowerInvariant();
    }

    public static PssSigner CreateRsaPkcs1Signer(string privateKeyPemPath)
    {
        using var reader = File.OpenText(privateKeyPemPath);
        var pemObj = new PemReader(reader).ReadObject();
        var key = pemObj as RsaPrivateCrtKeyParameters ?? throw new ArgumentException("Invalid RSA private key");
        var signer = new PssSigner(new RsaEngine(), new Sha256Digest(), 20);
        signer.Init(true, key);
        return signer;
    }

    public static byte[] ComputeSha256(byte[] data)
    {
        var d = new Sha256Digest();
        d.BlockUpdate(data, 0, data.Length);
        var result = new byte[d.GetDigestSize()];
        d.DoFinal(result, 0);
        return result;
    }
}


