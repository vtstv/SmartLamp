// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System.Security.Cryptography;
using System.Text;

namespace TuyaNet.Native.Crypto
{
    /// <summary>
    /// Cryptographic utilities for Tuya Local Protocol v3.1, v3.3, v3.4, and v3.5.
    /// </summary>
    public static class TuyaCrypto
    {
        /// <summary>
        /// Global well-known UDP broadcast key: MD5("yGAdlopoPVldABfn").
        /// </summary>
        public static readonly byte[] UdpKey = MD5.HashData(Encoding.UTF8.GetBytes("yGAdlopoPVldABfn"));

        /// <summary>
        /// Encrypts plaintext using AES-128 in ECB mode with PKCS7 padding.
        /// </summary>
        public static byte[] AesEcbEncrypt(byte[] plainBytes, byte[] keyBytes)
        {
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor();
            return enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        /// <summary>
        /// Decrypts ciphertext using AES-128 in ECB mode with PKCS7 padding.
        /// </summary>
        public static byte[] AesEcbDecrypt(byte[] cipherBytes, byte[] keyBytes)
        {
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }

        /// <summary>
        /// Encrypts plaintext using AES-128-GCM with associated authentication data (AAD).
        /// </summary>
        public static void AesGcmEncrypt(byte[] key, byte[] iv, byte[] plaintext, byte[] ciphertext, byte[] tag, byte[]? aad = null)
        {
            using var gcm = new AesGcm(key, 16);
            gcm.Encrypt(iv, plaintext, ciphertext, tag, aad);
        }

        /// <summary>
        /// Decrypts ciphertext using AES-128-GCM with associated authentication data (AAD).
        /// </summary>
        public static void AesGcmDecrypt(byte[] key, byte[] iv, byte[] ciphertext, byte[] tag, byte[] plaintext, byte[]? aad = null)
        {
            using var gcm = new AesGcm(key, 16);
            gcm.Decrypt(iv, ciphertext, tag, plaintext, aad);
        }

        /// <summary>
        /// Computes HMAC-SHA256 hash.
        /// </summary>
        public static byte[] ComputeHmacSha256(byte[] key, byte[] data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }
    }
}
