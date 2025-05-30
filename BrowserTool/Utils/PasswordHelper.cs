using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BrowserTool.Utils
{
    public static class PasswordHelper
    {
        private static readonly string PasswordFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_pwd.dat");
        private static readonly string Salt = "BrowserToolSalt2024";
        public static bool VerifyOrSetStartupPassword(string input)
        {
            if (!File.Exists(PasswordFile))
            {
                SaveStartupPassword(input);
                return true;
            }
            string hash = File.ReadAllText(PasswordFile);
            return hash == Hash(input);
        }
        public static void SaveStartupPassword(string pwd)
        {
            File.WriteAllText(PasswordFile, Hash(pwd));
        }
        private static string Hash(string pwd)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(pwd + Salt);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
} 