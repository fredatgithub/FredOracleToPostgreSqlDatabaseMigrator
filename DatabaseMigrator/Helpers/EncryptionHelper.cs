using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseMigrator.Helpers
{
    public class EncryptionHelper
    {
        private static readonly string Key = "DB_MIGRATOR_2025_KEY";  // Encryption key
        
        public static string Encrypt(string text)
        {
            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    using (var deriveBytes = new Rfc2898DeriveBytes(Key, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }))
                    {
                        aesAlg.Key = deriveBytes.GetBytes(32);
                        aesAlg.IV = deriveBytes.GetBytes(16);

                        using (MemoryStream msEncrypt = new MemoryStream())
                        {
                            using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
                            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(text);
                            }

                            return Convert.ToBase64String(msEncrypt.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string Decrypt(string cipherText)
        {
            try
            {
                if (string.IsNullOrEmpty(cipherText)) return string.Empty;

                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (Aes aesAlg = Aes.Create())
                {
                    using (var deriveBytes = new Rfc2898DeriveBytes(Key, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }))
                    {
                        aesAlg.Key = deriveBytes.GetBytes(32);
                        aesAlg.IV = deriveBytes.GetBytes(16);

                        using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                        using (ICryptoTransform decryptor = aesAlg.CreateDecryptor())
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
