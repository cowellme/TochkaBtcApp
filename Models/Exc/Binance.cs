using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Authentication;
using TochkaBtcApp.Contollers;

namespace TochkaBtcApp.Models.Exc;

public class Binance : IExchange
{
    private static string _sideDefault = "LONG";
    private static string _symbolDefault = "BTCUSDT";
    public void GetSignal(GlobalKlineInterval globalInterval)
    {
        var configs = ApplicationContext.GetConfigs();
        var users = ApplicationContext.GetUsers();

        if (configs == null) return;
        if (users == null) return;

        foreach (var config in configs)
        {
            var name = config.Name;
            var user = users.FirstOrDefault(x => x.Name == name);
            if (user != null)
            {
                var api = user.ApiBinance;
                var secret = user.SecretBinance;

                if (string.IsNullOrEmpty(api) && string.IsNullOrEmpty(secret)) return;

                var client = new BinanceRestClient();
                client.SetApiCredentials(new ApiCredentials(api, secret));
                var interval = ConvertToLocalKline(globalInterval);
                Buy(client, interval, config);
            }
        }
    }

    private static KlineInterval ConvertToLocalKline(GlobalKlineInterval interval)
    {
        var allValues = Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().ToList();
        return allValues.FirstOrDefault(x => (int)x == (int)interval);
    }
    private static async void Buy(BinanceRestClient client, KlineInterval interval, Config config)
    {
        try
        {
            var price = GetLastPrice(client, _symbolDefault);

            if (price > 0)
            {
                //if LONG
                var stopLoss = CalculateStopLoss(client, interval, price, config.CandlesCount, (decimal)config.OffsetMinimal, _sideDefault);

                var takeProfit = CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, _sideDefault);
                var stopLossPrice = price - stopLoss;
                takeProfit = Math.Round(takeProfit, 2);
                stopLossPrice = Math.Round(stopLossPrice, 2);
                var volume = (decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
                var qty = GetQuantityByBit(client, _symbolDefault, volume);



                var buyOrder = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                    symbol: _symbolDefault,
                    side: OrderSide.Buy,
                    type: FuturesOrderType.Market,
                    quantity: qty
                ).Result;

                if (buyOrder.Success)
                {
                    // Take Profit (тейк-профит) - рыночный
                    var takeProfitOrder = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: _symbolDefault,
                        side: OrderSide.Sell,  // противоположно входу (Buy → Sell)
                        type: FuturesOrderType.TakeProfitMarket,
                        quantity: qty,
                        stopPrice: takeProfit  // цена активации Take Profit
                    ).Result;

                    if (takeProfitOrder.Success)
                    {
                        // Stop Loss (стоп-лосс) - рыночный
                        var stopLossOrder = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol: _symbolDefault,
                            side: OrderSide.Sell,
                            type: FuturesOrderType.StopMarket,
                            quantity: qty,
                            stopPrice: stopLossPrice  // цена активации Stop Loss
                        ).Result;
                    }
                }
            }
        }
        catch (Exception e)
        {
            RLogger.Error(e);
        }
    }
    private static decimal GetLastPrice(BinanceRestClient client, string symbol)
    {
        try
        {
            var price = client.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol).Result.Data.Price;
            return price;
        }
        catch (Exception e)
        {
            RLogger.Error(e);
            return -1;
        }
    }
    public static decimal GetSymbolInfoAsync(BinanceRestClient client, string symbol)
    {

        // Получаем информацию о паре
        var symbolInfoResult = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().Result;
        if (!symbolInfoResult.Success)
        {
            Console.WriteLine($"Ошибка: {symbolInfoResult.Error}");
            return 0;
        }

        var btcusdtInfo = symbolInfoResult.Data.Symbols.FirstOrDefault(x => x.Name == symbol);
        if (btcusdtInfo != null)
        {
            return btcusdtInfo.MarketLotSizeFilter.StepSize;
        }
        else
        {
            Console.WriteLine("Пара BTCUSDT не найдена");
        }
        return 0;
    }
    private static decimal GetQuantityByBit(BinanceRestClient client, string symbol, decimal volume)
    {
        try
        {
            int round = GetDecimalPlaces(GetSymbolInfoAsync(client, symbol));

            var tickerResult = client.UsdFuturesApi.ExchangeData.GetTickerAsync(symbol).Result;

            if (!tickerResult.Success) RLogger.Error(new Exception($"Ошибка при получении цены: {tickerResult.Error}"));

            var ticker = tickerResult.Data;

            decimal price = ticker.LastPrice; // Текущая цена актива

            // var leverage = client.UsdFuturesApi.Account.GetSymbolConfigurationAsync().Result.Data.First().Leverage;
            // Рассчитываем количество криптовалюты
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
    private static int GetDecimalPlaces(decimal number)
    {
        // Преобразуем число в строку
        string numberString = number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Проверяем, есть ли точка (разделитель дробной части)
        if (numberString.Contains('.'))
        {
            // Возвращаем количество символов после точки
            return numberString.Split('.')[1].Length;
        }

        // Если точки нет, значит, знаков после запятой нет
        return 0;
    }
    private static decimal GetMaximumPriceForCandles(BinanceRestClient client, KlineInterval interval, int candlesCount)
    {
        var response = .0m;

        var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

        var max = listCandles?.MaxBy(x => x.HighPrice)?.ClosePrice;

        if (max != null) return (decimal)max;

        return response;
    }
    private static decimal GetMinimalPriceForCandles(BinanceRestClient client, KlineInterval interval, int candlesCount)
    {
        var response = .0m;

        var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

        var max = listCandles?.MinBy(x => x.LowPrice)?.ClosePrice;

        if (max != null) return (decimal)max;

        return response;
    }
    private static List<IBinanceKline>? GetLastCandles(BinanceRestClient client, string symbol, KlineInterval interval, int candlesCount)
    {
        try
        {
            var starDateTime = DateTime.UtcNow.AddSeconds((int)interval * -candlesCount);
            var endDateTime = DateTime.UtcNow;
            var result = client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, starDateTime, endDateTime).Result;
            if (result.Success)
            {
                var candles = result.Data.ToList();
                return candles;
            }

            return null;
        }
        catch (Exception e)
        {
            RLogger.Error(e);
            return null;
        }
    }
    private static decimal CalculateStopLoss(BinanceRestClient client, KlineInterval interval, decimal openPrice, int candlesCount, decimal offsetMinimal, string side)
    {
        var response = 0m;

        switch (side)
        {
            case "LONG":
                var minPrice = GetMinimalPriceForCandles(client, interval, candlesCount);
                if (minPrice > 0)
                {
                    response = Math.Abs(openPrice - minPrice) + minPrice * offsetMinimal;
                    //        200 == 90000 - 89900 + 100
                    break;
                }
                return -1;
            case "SHORT":
                var maxPrice = GetMaximumPriceForCandles(client, interval, candlesCount);
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
    public static decimal CalculateTakeProfit(decimal openPrice, decimal stopLoss, decimal riskRatio, string side)
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
    }
}