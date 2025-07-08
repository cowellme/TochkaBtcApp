using System.Text;

namespace BitUnixApi;

public static class SignatureGenerator
{
    public static string GenerateSign(
        string nonce,
        string timestamp,
        string apiKey,
        SortedDictionary<string, string>? queryParamsMap,
        string httpBody,
        string secretKey)
    {
        httpBody = httpBody.Replace(" ","");
        // 1. Собираем строку параметров запроса
        StringBuilder queryString = null;
        if (queryParamsMap != null && queryParamsMap.Count > 0)
        {
            queryString = new StringBuilder();
            foreach (var param in queryParamsMap)
            {
                if (param.Key == "sign")
                    continue;

                string value = param.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    queryString.Append(param.Key);
                    queryString.Append(value);
                }
            }
        }

        // 2. Формируем базовую строку для подписи
        string baseSignStr = nonce + timestamp + apiKey;

        if (queryString != null)
        {
            baseSignStr += queryString.ToString();
        }

        // 3. Вычисляем первый хеш (digest)
        string digest = Utils.Encrypt(baseSignStr + httpBody);

        // 4. Вычисляем финальную подпись
        return Utils.Encrypt(digest + secretKey);
    }
}