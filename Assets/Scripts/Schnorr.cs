using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using System;

public class Schnorr
{
    static private X9ECParameters secp256k1 = ECNamedCurveTable.GetByName("secp256k1");

    static private int PUBKEY_COMPRESSED_SIZE_BYTES = 33;

    static private byte[] ALG = System.Text.Encoding.Default.GetBytes("Schnorr+SHA256 ");

    static private int ENT_BITS = 256;

    static readonly SecureRandom secureRandom = new SecureRandom();

    public static Signature Sign(ECKeyPair kp, byte[] message)
    {
        SecureRandom drbg = GetDRBG(message);
        int len = secp256k1.N.BitLength / 8;
        byte[] bytes = new byte[len];
        drbg.NextBytes(bytes);

        Signature signature = null;
        while (signature == null)
        {
            BigInteger k = new BigInteger(1, bytes);
            signature = TrySign(kp, message, k);
        }

        return signature;
    }

    public static Signature TrySign(ECKeyPair kp, byte[] msg, BigInteger k)
    {
        BigInteger n = secp256k1.N;
        BigInteger privateKey = kp.privateKey;
        ECPoint publicKey = secp256k1.Curve.DecodePoint(kp.publicKey.ToByteArray());

        if (privateKey == (BigInteger.Zero))
        {
            throw new Exception("Private key must be >= 0");
        }

        if (privateKey.CompareTo(n) >= 0)
        {
            throw new Exception("Private key cannot be greater than curve order");
        }

        ECPoint Q = secp256k1.G.Multiply(k);

        BigInteger r = Hash(Q, publicKey, msg).Mod(secp256k1.N);

        if (r == (BigInteger.Zero))
        {
            return null;
        }

        BigInteger s = r.Multiply(privateKey).Mod(n);

        s = k.Subtract(s).Mod(n);

        if (s == (BigInteger.Zero))
        {
            return null;
        }

        return new Signature() { R = r, S = s };
    }

    static private BigInteger Hash(ECPoint q, ECPoint pubKey, byte[] msg)
    {
        int totalLength = PUBKEY_COMPRESSED_SIZE_BYTES * 2 + msg.Length;
        byte[] qCompressed = q.GetEncoded(true);
        byte[] pubKeyCompressed = pubKey.GetEncoded(true);
        byte[] hashInput = new byte[totalLength];

        Arrays.Fill(hashInput, (byte)0);
        Array.Copy(qCompressed, 0, hashInput, 0, PUBKEY_COMPRESSED_SIZE_BYTES);
        Array.Copy(pubKeyCompressed, 0, hashInput, PUBKEY_COMPRESSED_SIZE_BYTES, PUBKEY_COMPRESSED_SIZE_BYTES);
        Array.Copy(msg, 0, hashInput, PUBKEY_COMPRESSED_SIZE_BYTES * 2, msg.Length);

        System.Security.Cryptography.SHA256 sha = new System.Security.Cryptography.SHA256Managed();

        byte[] hash = sha.ComputeHash(hashInput);

        return new BigInteger(1, hash);
    }

    private static SecureRandom GetDRBG(byte[] message)
    {
        Sha256Digest sha256 = new Sha256Digest();
        HMac hMac = new HMac(sha256);
        return new SP800SecureRandomBuilder()
                .SetEntropyBitsRequired(ENT_BITS)
                .SetPersonalizationString(ALG)
                .BuildHMac(hMac, message, true);
    }
}