using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BitUnixApi;

public static class Utils
{
    public static string GetBody(object obj)
    {
        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        var ss = JsonConvert.SerializeObject(obj, settings);
        TochkaBtcApp.Models.Error.Log(new Exception("Request: " + ss));
        return ss;
    }
    public static string GetNonce()
    {
        return Guid.NewGuid().ToString().Replace("-", "");
    }
    public static string GetTimestamp()
    {
        // Получаем количество миллисекунд с 1 января 1970 года (Unix epoch)
        long milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return milliseconds.ToString();
    }
    public static string Encrypt(params string[] args)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            foreach (string arg in args)
            {
                if (!string.IsNullOrEmpty(arg))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(arg);
                    sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
            }

            sha256.TransformFinalBlock(new byte[0], 0, 0);

            return Byte2Hex(sha256.Hash);
        }
    }

    public static string Byte2Hex(byte[] bytes)
    {
        StringBuilder stringBuffer = new StringBuilder();

        foreach (byte b in bytes)
        {
            string temp = (b & 0xFF).ToString("x");

            if (temp.Length == 1)
            {
                stringBuffer.Append("0");
            }
            stringBuffer.Append(temp);
        }

        return stringBuffer.ToString();
    }
}