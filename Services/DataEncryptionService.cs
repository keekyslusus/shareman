using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GDriveTelegramSender.Services;

public static class DataEncryptionService
{
    // Magic header: "GDTE" (GDrive Telegram Encrypted) + version byte 0x01
    private static readonly byte[] MagicHeader = new byte[] { 0x47, 0x44, 0x54, 0x45, 0x01 };

    // Application salt and seed for key derivation (portable across devices without user password prompt)
    private static readonly byte[] AppSalt = new byte[]
    {
        0x7E, 0x2A, 0x9B, 0x31, 0x54, 0xC8, 0x61, 0xDF,
        0x10, 0xAB, 0x47, 0x8C, 0xF3, 0x29, 0x50, 0xE4
    };

    private static readonly byte[] EncryptionKey = DeriveKey();

    private static byte[] DeriveKey()
    {
        byte[] seed = Encoding.UTF8.GetBytes("GDriveTelegramSender_PortableSecret_v1_c49f82d1");
        return Rfc2898DeriveBytes.Pbkdf2(seed, AppSalt, 10000, HashAlgorithmName.SHA256, 32);
    }

    public static bool IsEncrypted(byte[] data)
    {
        if (data.Length < MagicHeader.Length) return false;
        for (int i = 0; i < MagicHeader.Length; i++)
        {
            if (data[i] != MagicHeader[i]) return false;
        }
        return true;
    }

    public static byte[] Encrypt(byte[] plaintext)
    {
        // Format: [MagicHeader: 5 bytes] [Nonce: 12 bytes] [Tag: 16 bytes] [Ciphertext: N bytes]
        byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
        byte[] ciphertext = new byte[plaintext.Length];

        using var aesGcm = new AesGcm(EncryptionKey, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, MagicHeader);

        using var ms = new MemoryStream(MagicHeader.Length + nonce.Length + tag.Length + ciphertext.Length);
        ms.Write(MagicHeader, 0, MagicHeader.Length);
        ms.Write(nonce, 0, nonce.Length);
        ms.Write(tag, 0, tag.Length);
        ms.Write(ciphertext, 0, ciphertext.Length);
        return ms.ToArray();
    }

    public static byte[] Decrypt(byte[] payload)
    {
        if (!IsEncrypted(payload))
        {
            return payload;
        }

        int headerLen = MagicHeader.Length;
        int nonceLen = AesGcm.NonceByteSizes.MaxSize; // 12
        int tagLen = AesGcm.TagByteSizes.MaxSize; // 16

        if (payload.Length < headerLen + nonceLen + tagLen)
        {
            throw new InvalidDataException("Invalid encrypted payload length.");
        }

        byte[] nonce = new byte[nonceLen];
        Buffer.BlockCopy(payload, headerLen, nonce, 0, nonceLen);

        byte[] tag = new byte[tagLen];
        Buffer.BlockCopy(payload, headerLen + nonceLen, tag, 0, tagLen);

        int ciphertextLen = payload.Length - headerLen - nonceLen - tagLen;
        byte[] ciphertext = new byte[ciphertextLen];
        Buffer.BlockCopy(payload, headerLen + nonceLen + tagLen, ciphertext, 0, ciphertextLen);

        byte[] plaintext = new byte[ciphertextLen];
        using var aesGcm = new AesGcm(EncryptionKey, tagLen);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, MagicHeader);

        return plaintext;
    }
}
