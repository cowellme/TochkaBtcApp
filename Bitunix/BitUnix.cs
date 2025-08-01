﻿using BingX.Net.Clients;
using BingX.Net.Objects.Models;
using BitUnixApi;
using BitUnixApi.Enums;
using CryptoExchange.Net.Requests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Telegram.Bot.Types;
using TochkaBtcApp.Bitunix.Enums;
using TochkaBtcApp.Bitunix.Models;
using TochkaBtcApp.Contollers;
using TochkaBtcApp.Models;
using TochkaBtcApp.Models.Exc;

public class BitUnix : IExchange
{
    private readonly HttpClient _httpClient = new HttpClient();
    private static string _sideDefault = "LONG";
    private static string _symbolDefault = "BTCUSDT";


    public HttpRequestMessage? CreateHeaders(BitUnixOrder order,AppUser user)
    {
        var api = user.ApiBitUnix;
        var secret = user.SecretBitUnix;

        if (string.IsNullOrEmpty(api) || string.IsNullOrEmpty(secret)) return null;

        var timestamp = Utils.GetTimestamp();
        var nonce = Utils.GetNonce();
        var httpBody = Utils.GetBody(order);
        SortedDictionary<string, string>? queryParamsMap = null;
        var sign = SignatureGenerator.GenerateSign(nonce, timestamp, api, queryParamsMap, httpBody, secret);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://fapi.bitunix.com/api/v1/futures/trade/place_order"),
            Content = new StringContent(httpBody),
        };
        request.Headers.Add("api-key", api);
        request.Headers.Add("sign", sign);
        request.Headers.Add("nonce", nonce);
        request.Headers.Add("timestamp", timestamp);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return request;
    }
    public async Task<string> PlaceHOrder(BitUnixOrder order, AppUser user)
    {
        try
        {
            return await Task.Run(() =>
            {
                var request = CreateHeaders(order, user);
                if (request is null) return null;
                
                using var response = _httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                var price = GetLastPrice(order.symbol).Result;
                if (decimal.TryParse(order.qty, out var deQty))
                {
                    user.SendAlert($"{order.side} {order.symbol}\n" +
                                   $"Take: $ {order.tpPrice}\n" +
                                   $"Open: $ {price:0.00}\n" +
                                   $"Stop: $ {order.slPrice}" +
                                   $"Volume: $ {(deQty * price):0.00}");
                }
                
                var result = response.Content.ReadAsStringAsync();
                return result;
            });
        }
        catch (HttpRequestException e)
        {
            return $"Error: {e.Message}";
        }
    }
    public async Task<string> PlaceOrder(BitUnixOrder order, AppUser user)
    {
        try
        {
            return await Task.Run(() =>
            {
                var timestamp = Utils.GetTimestamp();
                var nonce = Utils.GetNonce(); 
                var httpBody = Utils.GetBody(order);
                SortedDictionary<string, string>? queryParamsMap = null;
                var sign = SignatureGenerator.GenerateSign(nonce, timestamp, user.ApiBitUnix, queryParamsMap, httpBody, user.SecretBitUnix);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post, 
                    RequestUri = new Uri($"https://fapi.bitunix.com/api/v1/futures/trade/place_order"), 
                    Content = new StringContent(httpBody),
                };
                request.Headers.Add("api-key", user.ApiBitUnix);
                request.Headers.Add("sign", sign);
                request.Headers.Add("nonce", nonce);
                request.Headers.Add("timestamp", timestamp);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                using var response = _httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                var result = response.Content.ReadAsStringAsync();
                return result;
            });
        }
        catch (HttpRequestException e)
        {
            return $"Error: {e.Message}";
        }
    }
    private async Task<(decimal stopLossPrice, decimal takeProfitPrice, decimal quantity)?> Calculated( KlineInterval interval, Config config, decimal price, string symbol = "BTCUSDT")
    {
        try
        {
            var stopLoss = await CalculateStopLoss(interval, price, config.CandlesCount, (decimal)config.OffsetMinimal / 100, _sideDefault, symbol);

            var takeProfitPrice = await CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, _sideDefault);
            var stopLossPrice = price - stopLoss;
            takeProfitPrice = Math.Round(takeProfitPrice, 2);
            stopLossPrice = Math.Round(stopLossPrice, 2);
            var volume = (decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
            var qty = await GetQuantity(symbol, volume, price);
            var response = (stopLossPrice, takeProfitPrice, qty);
            return response;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    private async Task<List<BitUnixFuturesKline>?> GetLastCandles(string symbol, KlineInterval interval, int candlesCount)
    {
        try
        {
            var starDateTime = DateTimeOffset.UtcNow.AddSeconds((int)interval * -candlesCount).ToUnixTimeMilliseconds();
            var endDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var result = await GetKlinesAsync(symbol, interval, starDateTime, candlesCount);
            if (result != null)
            {
                return result;
            }

            return null;
        }
        catch (Exception e)
        {
            RLogger.Error(e);
            return null;
        }
    }
    private async Task<List<BitUnixFuturesKline>?> GetKlinesAsync(string symbol, KlineInterval interval, long starDateTime, int candelsCount)
    {
        try
        {
            string intervalString = IntervalStringFrom(interval);
            var uri =
                $"https://fapi.bitunix.com/api/v1/futures/market/kline?symbol={symbol}&startTime={starDateTime}&interval={intervalString}&limit={candelsCount}";
            var response = await _httpClient.GetAsync(uri);

            response.EnsureSuccessStatusCode(); // Проверяем успешность запроса
            var settings = new JsonSerializerSettings
            {
                // Указываем культуру, где точка — разделитель дробной части
                Culture = CultureInfo.InvariantCulture,
                // Если число передано как строка ("123.45"), это поможет
                FloatParseHandling = FloatParseHandling.Decimal
            };
            var data = await response.Content.ReadAsStringAsync();
            var responseBitUnixFutures = JsonConvert.DeserializeObject<ResponseBitUnixFuturesKline>(data, settings);

            if (responseBitUnixFutures == null || responseBitUnixFutures.data == null)
                return null;

            return responseBitUnixFutures.data;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    private string IntervalStringFrom(KlineInterval interval)
    {
        if (interval == KlineInterval.FiveMinutes) return "5m";
        if (interval == KlineInterval.FifteenMinutes) return "15m";
        if (interval == KlineInterval.OneHour) return "1h";
        if (interval == KlineInterval.FourHours) return "4h";
        return "";
    }
    public async Task<decimal> CalculateTakeProfit(decimal openPrice, decimal stopLoss, decimal riskRatio, string side)
    {
        return await Task.Run(() =>
        {
            var response = .0m;

            switch (side)
            {
                case "LONG":
                    response = (openPrice - (openPrice - stopLoss)) * riskRatio + openPrice;
                    break;

                case "SHORT":
                    response = (openPrice - (openPrice + stopLoss)) * riskRatio + openPrice;
                    break;
            }

            return response;
        });
    }
    private async Task<decimal> GetMaximumPriceForCandles( KlineInterval interval, int candlesCount)
    {
        var response = .0m;

        var listCandles = await GetLastCandles(_symbolDefault, interval, candlesCount);

        var max = listCandles?.MaxBy(x => x.high)?.high;

        if (max != null) return (decimal)max;

        return response;
    }
    private async Task<decimal> GetMinimalPriceForCandles(KlineInterval interval, int candlesCount, string symbol)
    {
        var response = .0m;

        var listCandles = await GetLastCandles(symbol, interval, candlesCount);

        var min = listCandles?.MinBy(x => x.low)?.low;

        if (min != null) return (decimal)min;

        return response;
    }
    private async Task<decimal> CalculateStopLoss(KlineInterval interval, decimal openPrice, int candlesCount, decimal offsetMinimal, string side, string symbol)
    {
        var response = 0m;

        switch (side)
        {
            case "LONG":
                var minPrice = await GetMinimalPriceForCandles(interval, candlesCount, symbol);
                if (minPrice > 0)
                {
                    response = Math.Abs(openPrice - minPrice) + minPrice * offsetMinimal;
                    //        200 == 90000 - 89900 + 100
                    break;
                }
                return -1;
            case "SHORT":
                var maxPrice = await GetMaximumPriceForCandles(interval, candlesCount);
                if (maxPrice > 0)
                {
                    response = Math.Abs(openPrice - maxPrice) + maxPrice * offsetMinimal;
                    //        200 == 90000 - 90100 + 100
                    break;
                }
                return -1;
        }

        return response;
    }
    private async Task<int?> GetSymbolInfoAsync( string symbol)
    {

        try
        {
            var response = await _httpClient.GetAsync(
                $"https://fapi.bitunix.com/api/v1/futures/market/trading_pairs?symbol");

            response.EnsureSuccessStatusCode(); // Проверяем успешность запроса

            var data = await response.Content.ReadAsStringAsync();
            var tradingPairResponse = JsonConvert.DeserializeObject<TradingPairResponse>(data);

            if (tradingPairResponse == null || tradingPairResponse.data == null)
                return null;

            var curSym = tradingPairResponse.data.FirstOrDefault(x => x.symbol == symbol)?.basePrecision ?? null;
            return curSym;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    private async Task<decimal> GetQuantity( string symbol, decimal volume, decimal price)
    {
        try
        {
            int round = await GetSymbolInfoAsync(symbol) ?? -1;

            if (round < 0) return 0;

            if (price < 0) return 0;

            decimal amount = volume / price;

            var response = Math.Round(amount, round);

            return response;
        }
        catch (Exception ex)
        {
            RLogger.Error(ex);
            throw;
        }
    }
    public async Task<string> GetSignal(GlobalKlineInterval globalInterval)
    {
        try
        {
            var configs = ApplicationContext.GetConfigs();
            var users = ApplicationContext.GetUsers();

            if (configs == null) throw new Exception("Отсутствуют подходящие конфиги");
            if (users == null) throw new Exception("Отсутствуют подходящие пользователи");

            foreach (var config in configs)
            {
                if (config.TimeFrame == globalInterval.ToString())
                {
                    var name = config.Name;
                    var user = users.FirstOrDefault(x => x.Name == name);

                    if (user != null)
                    {
                        var api = user.ApiBitUnix;
                        var secret = user.SecretBitUnix;

                        if (string.IsNullOrEmpty(api) && string.IsNullOrEmpty(secret)) continue;

                        var interval = ConvertToLocalKline(globalInterval);
                        var result = await Buy(user, interval, config);
                        Error.Log(new Exception(result));
                    }
                }
            }
            return "Успешная обработка сигнала!";
        }
        catch (Exception e)
        {
            Error.Log(e);
            return e.Message;
        }
    }
    public async Task<List<Pair>?> GetAllPairs()
    {
        try
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://fapi.bitunix.com/api/v1/futures/market/trading_pairs?asset=USDT")
            };

            using var response = _httpClient.SendAsync(request).Result;
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(result);
            if (json.TryGetValue("data", out var field))
            {
                var jsString = field.ToString();
                var pairsObj = JsonConvert.DeserializeObject<List<Pair>>(jsString);
                return pairsObj;
            }
            return new List<Pair>();
        }
        catch (Exception e)
        {
            e.Source = "GetAllPairs";
            Error.Log(e);
            return null;
        }
    }
    private async Task<string> Buy(AppUser user, KlineInterval interval, Config config)
    {
        try
        {
            var price = await GetLastPrice(_symbolDefault) ?? -1;

            if (price < 0) throw new Exception("Ошибка получения цены");

            var calculated = await Calculated(interval, config, price);

            if (calculated == null) throw new Exception("Ошибка расчетов позиции");
            var quantity = calculated?.quantity ?? -1; 
            var tpPrice = calculated?.takeProfitPrice ?? -1;
            var slPrice = calculated?.stopLossPrice ?? -1;
            var order = new BitUnixOrder
            {
                symbol = _symbolDefault,
                side = BitUnixSide.Buy,
                orderType = BitUnixOrderType.Market,
                qty = quantity.ToString(CultureInfo.InvariantCulture),
                tpPrice = tpPrice.ToString(CultureInfo.InvariantCulture),
                slPrice = slPrice.ToString(CultureInfo.InvariantCulture),
            };

            var result = await PlaceOrder(order, user);
            return result;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return e.Message;
        }
    }
    private async Task<decimal?> GetLastPrice(string symbolDefault)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://openapi.bitunix.com/api/spot/v1/market/last_price?symbol={symbolDefault}");

            response.EnsureSuccessStatusCode(); // Проверяем успешность запроса

            var data = await response.Content.ReadAsStringAsync();
            var lastPriceResponse = JsonConvert.DeserializeObject<LastPriceResponse>(data);

            if (lastPriceResponse == null || lastPriceResponse.data == null)
                return null;

            if (decimal.TryParse(lastPriceResponse.data.Replace(".",","), out var result))
            {
                return result;
            }

            return null;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    private KlineInterval ConvertToLocalKline(GlobalKlineInterval globalInterval)
    {
        var allValues = Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().ToList();
        return allValues.FirstOrDefault(x => (int)x == (int)globalInterval);
    }
    public async Task<string> BuyHSignal(AppUser user, hSignal signal)
    {
        try
        {
            var config = signal.Config;
            config.TimeFrame = signal.TimeFrame;
            var order = await OrderFromSignal(signal);
            if (order == null) throw new Exception("399:1 BuyHSignal");
            var result = await PlaceHOrder(order, user);
            return result;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return e.Message;
        }
    }
    private async Task<BitUnixOrder?> OrderFromSignal(hSignal signal)
    {
        try
        { 
            string symbol = signal.Symbol;
            var price = await GetLastPrice(symbol) ?? -1;
            if (price < 0) throw new Exception("Price is not found");
            var side = signal.Side.ToLower() == "long" ? BitUnixSide.Buy : BitUnixSide.Sell;
            var config = signal.Config;
            var interval = ParseInterval(signal.TimeFrame);
            var calculated = Calculated(interval, config, price, symbol).Result;

            if (calculated == null) throw new Exception("Ошибка расчетов позиции");
            var quantity = calculated?.quantity ?? -1;
            var tpPrice = calculated?.takeProfitPrice ?? -1;
            var slPrice = calculated?.stopLossPrice ?? -1;
            var order = new BitUnixOrder
            {
                symbol = symbol,
                side = side,
                orderType = BitUnixOrderType.Market,
                qty = quantity.ToString(CultureInfo.InvariantCulture),
                tpPrice = tpPrice.ToString(CultureInfo.InvariantCulture),
                slPrice = slPrice.ToString(CultureInfo.InvariantCulture),
            };
            return order;
        }
        catch (Exception e)
        {
            e.Source = "418:1 OrderByComfig";
            Error.Log(e);
            return null;
        }
    }
    private KlineInterval ParseInterval(string signalTimeFrame)
    {
        switch (signalTimeFrame)
        {
            case "OneMinute":
                return KlineInterval.OneMinute;
            case "ThreeMinutes":
                return KlineInterval.ThreeMinutes;
            case "FiveMinutes":
                return KlineInterval.FiveMinutes;
            case "FifteenMinutes":
                return KlineInterval.FifteenMinutes;
            case "ThirtyMinutes":
                return KlineInterval.ThirtyMinutes;
            case "OneHour":
                return KlineInterval.OneHour;
            case "TwoHours":
                return KlineInterval.TwoHours;
            case "FourHours":
                return KlineInterval.FourHours;
            case "SixHours":
                return KlineInterval.SixHours;
            case "EightHours":
                return KlineInterval.EightHours;
            case "TwelveHours":
                return KlineInterval.TwelveHours;
            case "OneDay":
                return KlineInterval.OneDay;
            case "ThreeDay":
                return KlineInterval.ThreeDay;
            case "OneWeek":
                return KlineInterval.OneWeek;
            case "OneMonth":
                return KlineInterval.OneMonth;
            default: 
                return KlineInterval.FiveMinutes;
        }
    }
    public static HttpRequestMessage? CreateHeadersAccount(AppUser user, string url = @"https://fapi.bitunix.com/api/v1/futures/account?marginCoin=USDT", object? obj = null)
    {
        var api = user.ApiBitUnix;
        var secret = user.SecretBitUnix;

        if (string.IsNullOrEmpty(api) || string.IsNullOrEmpty(secret)) return null;

        var timestamp = Utils.GetTimestamp();
        var nonce = Utils.GetNonce();
        
        SortedDictionary<string, string>? queryParamsMap = new SortedDictionary<string, string> { { "marginCoin", "USDT" } };
        var sign = SignatureGenerator.GenerateSign(nonce, timestamp, api, queryParamsMap, "", secret);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
        };
        request.Headers.Add("api-key", api);
        request.Headers.Add("sign", sign);
        request.Headers.Add("nonce", nonce);
        request.Headers.Add("timestamp", timestamp);
        return request;
    }
    public static async Task<string> GetBalance(AppUser user)
    {
        //https://fapi.bitunix.com/api/v1/futures/account?marginCoin=USDT
        var request = CreateHeadersAccount(user);
        var cl = new HttpClient();
        using var response = cl.SendAsync(request).Result;
        response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadAsStringAsync();
        var account = JsonConvert.DeserializeObject<ResponseAccount>(res).data;
        return account.available;
    }
}

internal class BitUnixFuturesKline
{
    public decimal open { get; set; }
    public decimal high { get; set; }
    public decimal close { get; set; }
    public decimal low { get; set; }
    public decimal time { get; set; }
    public decimal quoteVol { get; set; }
    public decimal baseVol { get; set; }
    public string type { get; set; }
}
internal class ResponseBitUnixFuturesKline
{
    public int code { get; set; }
    public List<BitUnixFuturesKline> data { get; set; }
    public string msg { get; set; }
}
public class TradingPairResponse
{
    public string code { get; set; }
    public List<TradingPair> data { get; set; }
    public string msg { get; set; }
}
public class TradingPair
{
    public string symbol { get; set; }
    public string @base { get; set; }
    public string quote { get; set; }
    public string minTradeVolume { get; set; }
    public string minBuyPriceOffset { get; set; }
    public string maxSellPriceOffset { get; set; }
    public string maxLimitOrderVolume { get; set; }
    public string maxMarketOrderVolume { get; set; }
    public int basePrecision { get; set; }
    public int quotePrecision { get; set; }
    public int maxLeverage { get; set; }
    public int minLeverage { get; set; }
    public int defaultLeverage { get; set; }
    public string defaultMarginMode { get; set; }
    public string priceProtectScope { get; set; }
    public string symbolStatus { get; set; }
}