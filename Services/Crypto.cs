using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

public static class Crypto
{
    private const int SaltSize = 16;
    private const int Iterations = 10000;
    private const int KeySize = 32;

    public static string Encrypt(string plainText, string password)
    {
        byte[] salt = GenerateRandomSalt();
        byte[] key = GenerateKey(password, salt);
        byte[] iv = GenerateRandomIV();

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(salt, 0, salt.Length);
                ms.Write(iv, 0, iv.Length);

                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (StreamWriter sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                // Конвертируем в Base64 и убираем ненужные символы
                string base64 = Convert.ToBase64String(ms.ToArray());
                return RemoveSpecialChars(base64);
            }
        }
    }

    public static string Decrypt(string encryptedText, string password)
    {
        // Восстанавливаем Base64 (добавляем обратно '=')
        string base64 = RestoreBase64(encryptedText);
        byte[] encryptedBytes = Convert.FromBase64String(base64);

        byte[] salt = new byte[SaltSize];
        Array.Copy(encryptedBytes, 0, salt, 0, SaltSize);

        byte[] iv = new byte[16];
        Array.Copy(encryptedBytes, SaltSize, iv, 0, 16);

        byte[] key = GenerateKey(password, salt);

        int cipherStart = SaltSize + 16;
        int cipherLength = encryptedBytes.Length - cipherStart;
        byte[] cipherBytes = new byte[cipherLength];
        Array.Copy(encryptedBytes, cipherStart, cipherBytes, 0, cipherLength);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream(cipherBytes))
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (StreamReader sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }

    // Убираем из Base64 символы '+', '/', '='
    private static string RemoveSpecialChars(string base64)
    {
        return new string(base64
            .Replace('+', '-')
            .Replace('/', '_')
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());
    }

    // Восстанавливаем Base64 (заменяем '-' на '+', '_' на '/', добавляем '=')
    private static string RestoreBase64(string modified)
    {
        string base64 = modified
            .Replace('-', '+')
            .Replace('_', '/');

        // Добавляем недостающие '='
        int padding = base64.Length % 4;
        if (padding > 0)
            base64 += new string('=', 4 - padding);

        return base64;
    }

    private static byte[] GenerateRandomSalt()
    {
        byte[] salt = new byte[SaltSize];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        return salt;
    }

    private static byte[] GenerateRandomIV()
    {
        byte[] iv = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }
        return iv;
    }

    private static byte[] GenerateKey(string password, byte[] salt)
    {
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(KeySize);
        }
    }
}