// ============================================================================
// Copyright (c) 2026 Murr (https://github.com/vtstv). All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Security.Cryptography;
using System.Text;
using TuyaNet.Native.Crypto;
using Xunit;

namespace SmartLamp.Tests
{
    public class TuyaCryptoTests
    {
        [Fact]
        public void AesEcb_EncryptAndDecrypt_RoundTrip_MatchesOriginal()
        {
            byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef"); // 16 bytes
            string plainText = "{\"dps\":{\"20\":true,\"21\":\"white\"}}";
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] cipher = TuyaCrypto.AesEcbEncrypt(plainBytes, key);
            Assert.NotNull(cipher);
            Assert.True(cipher.Length > 0);
            Assert.True(cipher.Length % 16 == 0); // PKCS7 padded block size

            byte[] decrypted = TuyaCrypto.AesEcbDecrypt(cipher, key);
            string result = Encoding.UTF8.GetString(decrypted);

            Assert.Equal(plainText, result);
        }

        [Fact]
        public void AesEcb_KnownVector_EncryptsAndDecryptsCorrectly()
        {
            byte[] key = Encoding.UTF8.GetBytes("1234567890123456");
            byte[] input = Encoding.UTF8.GetBytes("Hello, Tuya!");

            byte[] encrypted = TuyaCrypto.AesEcbEncrypt(input, key);
            byte[] decrypted = TuyaCrypto.AesEcbDecrypt(encrypted, key);

            Assert.Equal(input, decrypted);
        }

        [Fact]
        public void AesGcm_EncryptAndDecrypt_RoundTrip_WithAad()
        {
            byte[] key = Encoding.UTF8.GetBytes("abcdef0123456789");
            byte[] iv = Encoding.UTF8.GetBytes("123456789012"); // 12 bytes
            byte[] aad = Encoding.UTF8.GetBytes("HEADER_AAD_DATA_");
            byte[] plainBytes = Encoding.UTF8.GetBytes("{\"commands\":[{\"code\":\"switch_led\",\"value\":true}]}");

            byte[] cipher = new byte[plainBytes.Length];
            byte[] tag = new byte[16];

            TuyaCrypto.AesGcmEncrypt(key, iv, plainBytes, cipher, tag, aad);

            byte[] decrypted = new byte[cipher.Length];
            TuyaCrypto.AesGcmDecrypt(key, iv, cipher, tag, decrypted, aad);

            Assert.Equal(plainBytes, decrypted);
        }

        [Fact]
        public void AesGcm_TamperedCiphertext_ThrowsCryptographicException()
        {
            byte[] key = Encoding.UTF8.GetBytes("abcdef0123456789");
            byte[] iv = Encoding.UTF8.GetBytes("123456789012");
            byte[] aad = Encoding.UTF8.GetBytes("HEADER_AAD_DATA_");
            byte[] plainBytes = Encoding.UTF8.GetBytes("{\"switch_led\":false}");

            byte[] cipher = new byte[plainBytes.Length];
            byte[] tag = new byte[16];

            TuyaCrypto.AesGcmEncrypt(key, iv, plainBytes, cipher, tag, aad);

            // Corrupt one byte of ciphertext
            cipher[0] ^= 0xFF;

            byte[] decrypted = new byte[cipher.Length];
            Assert.ThrowsAny<CryptographicException>(() =>
            {
                TuyaCrypto.AesGcmDecrypt(key, iv, cipher, tag, decrypted, aad);
            });
        }

        [Fact]
        public void ComputeHmacSha256_ComputesCorrectDigest()
        {
            byte[] key = Encoding.UTF8.GetBytes("secret_local_key");
            byte[] data = Encoding.UTF8.GetBytes("nonce_random_client_payload");

            byte[] hmac = TuyaCrypto.ComputeHmacSha256(key, data);
            Assert.NotNull(hmac);
            Assert.Equal(32, hmac.Length);

            using var stdHmac = new HMACSHA256(key);
            byte[] expected = stdHmac.ComputeHash(data);

            Assert.Equal(expected, hmac);
        }

        [Fact]
        public void UdpKey_IsWellKnownTuyaKey()
        {
            Assert.NotNull(TuyaCrypto.UdpKey);
            Assert.Equal(16, TuyaCrypto.UdpKey.Length);

            byte[] expected = MD5.HashData(Encoding.UTF8.GetBytes("yGAdlopoPVldABfn"));
            Assert.Equal(expected, TuyaCrypto.UdpKey);
        }
    }
}
