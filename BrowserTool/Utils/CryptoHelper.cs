using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace BrowserTool.Utils
{
    public static class KeyManager
    {
        private static string _md5Key = null;
        public static void SetPassword(string password)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
                _md5Key = BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        public static string GetAesKey(int keyLen = 32)
        {
            if (string.IsNullOrEmpty(_md5Key)) throw new Exception("未登录！");
            var key = _md5Key.Substring(1); // 从第2位开始
            if (key.Length < keyLen)
                key = key.PadRight(keyLen, '0');
            else
                key = key.Substring(0, keyLen);
            return key;
        }
        public static void Clear() { _md5Key = null; }
    }

    public static class CryptoHelper
    {
        private static readonly string IV = "BrowserTool2024";  // 16字节IV

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            using (Aes aes = Aes.Create())
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(KeyManager.GetAesKey(32));
                byte[] ivBytes = Encoding.UTF8.GetBytes(IV.PadRight(16, '0').Substring(0, 16));
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            // 检查是否是有效的Base64字符串
            if (!IsValidBase64String(cipherText))
            {
                System.Diagnostics.Debug.WriteLine($"无效的Base64字符串: {cipherText}");
                return cipherText;
            }

            try
            {
                using (Aes aes = Aes.Create())
                {
                    byte[] keyBytes = Encoding.UTF8.GetBytes(KeyManager.GetAesKey(32));
                    byte[] ivBytes = Encoding.UTF8.GetBytes(IV.PadRight(16, '0').Substring(0, 16));
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"解密失败（格式错误）: {ex.Message}, 原文: {cipherText}");
                return cipherText;
            }
            catch (CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"解密失败（加密错误）: {ex.Message}, 原文: {cipherText}");
                return cipherText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解密失败（其他错误）: {ex.Message}, 原文: {cipherText}");
                return cipherText;
            }
        }

        private static bool IsValidBase64String(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;

            // 检查长度是否为4的倍数
            if (str.Length % 4 != 0) return false;

            // 检查是否只包含有效的Base64字符
            foreach (char c in str)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '='))
                {
                    return false;
                }
            }

            // 检查填充字符
            int paddingCount = str.Count(c => c == '=');
            if (paddingCount > 2) return false;

            return true;
        }
    }
} 