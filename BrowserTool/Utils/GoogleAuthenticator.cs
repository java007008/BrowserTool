using System;
using System.Security.Cryptography;
using System.Text;

namespace BrowserTool.Utils
{
    public static class GoogleAuthenticator
    {
        public static string GenerateCode(string secret)
        {
            byte[] key = Base32Decode(secret.Replace(" ", "").ToUpper());
            long timestep = GetCurrentUnixTimestamp() / 30;
            byte[] timestepBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(timestep));
            using (var hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(timestepBytes);
                int offset = hash[hash.Length - 1] & 0x0F;
                int code = ((hash[offset] & 0x7F) << 24)
                         | ((hash[offset + 1] & 0xFF) << 16)
                         | ((hash[offset + 2] & 0xFF) << 8)
                         | (hash[offset + 3] & 0xFF);
                return (code % 1000000).ToString("D6");
            }
        }
        private static long GetCurrentUnixTimestamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
        private static byte[] Base32Decode(string base32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            int bits = 0, value = 0, index = 0;
            var output = new byte[base32.Length * 5 / 8];
            foreach (char c in base32)
            {
                if (c == '=') break;
                int i = alphabet.IndexOf(c);
                if (i < 0) throw new ArgumentException("Invalid base32");
                value = (value << 5) | i;
                bits += 5;
                if (bits >= 8)
                {
                    output[index++] = (byte)((value >> (bits - 8)) & 0xFF);
                    bits -= 8;
                }
            }
            Array.Resize(ref output, index);
            return output;
        }
    }
} 