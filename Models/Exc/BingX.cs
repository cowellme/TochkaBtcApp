using BingX.Net.Clients;
using BingX.Net.Enums;
using BingX.Net.Objects.Models;
using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.DataProtection;
using TochkaBtcApp.Components;
using TochkaBtcApp.Contollers;
using TochkaBtcApp.Telegram;

namespace TochkaBtcApp.Models.Exc
{
    public class BingX 
    {
        private static string _exchangeName = "bingx";
        private static string _sideDefault = "LONG";
        private static string _symbolDefault = "BTC-USDT";
        private static List<Order> _orders = new List<Order>();
        private static List<AppUser> _users = new List<AppUser>();

        private static BingXRestClient? GetClient(AppUser user)
        {
            try
            {
                var newClient = new BingXRestClient();

                newClient.SetApiCredentials(new ApiCredentials(user.ApiBingx, user.SecretBingx));

                return newClient;
            }
            catch (Exception e)
            {
                Error.Log(e);
                return null;
            }
        }
        private static KlineInterval ConvertToLocalKline(GlobalKlineInterval interval)
        {
            var allValues = Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().ToList();
            return allValues.FirstOrDefault(x => (int)x == (int)interval);
        }
        private static decimal GetLastPrice(BingXRestClient client, string symbol)
        {
            try
            {
                var price = client.PerpetualFuturesApi.ExchangeData.GetTickerAsync(symbol).Result.Data.LastPrice;
                return price;
            }
            catch (Exception e)
            {
                RLogger.Error(e);
                return -1;
            }
        }
        private static decimal CalculateStopLoss(BingXRestClient client, KlineInterval interval, decimal openPrice, int candlesCount, decimal offsetMinimal, string side)
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
        private static decimal GetMaximumPriceForCandles(BingXRestClient client, KlineInterval interval, int candlesCount)
        {
            var response = .0m;

            var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

            var max = listCandles?.MaxBy(x => x.HighPrice)?.HighPrice;

            if (max != null) return (decimal)max;

            return response;
        }
        private static decimal GetMinimalPriceForCandles(BingXRestClient client, KlineInterval interval, int candlesCount)
        {
            var response = .0m;

            var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

            var min = listCandles?.MinBy(x => x.LowPrice)?.LowPrice;

            if (min != null) return (decimal)min;

            return response;
        }
        private static List<BingXFuturesKline>? GetLastCandles(BingXRestClient client, string symbol, KlineInterval interval, int candlesCount)
        {
            try
            {
                var starDateTime = DateTime.UtcNow.AddSeconds((int)interval * -candlesCount);
                var endDateTime = DateTime.UtcNow;
                var result = client.PerpetualFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, starDateTime, endDateTime).Result;
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
        public void GetSignal(GlobalKlineInterval globalInterval)
        {
            try
            {
                var configs = ApplicationContext.GetConfigs();
                var users = ApplicationContext.GetUsers();

                if (configs == null) return;
                if (users == null) return;

                foreach (var config in configs)
                {
                    if (config.TimeFrame == globalInterval.ToString())
                    {
                        var name = config.Name;
                        var user = users.FirstOrDefault(x => x.Name == name);

                        if (user != null)
                        {
                            var api = user.ApiBingx;
                            var secret = user.SecretBingx;

                            if (string.IsNullOrEmpty(api) && string.IsNullOrEmpty(secret)) continue;

                            var interval = ConvertToLocalKline(globalInterval);
                            Buy(user, interval, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }
        private static (decimal stopLossPrice, decimal takeProfitPrice, decimal quantity) Calculated(BingXRestClient client, KlineInterval interval, Config config, decimal price)
        {

            var stopLoss = CalculateStopLoss(client, interval, price, config.CandlesCount, (decimal)config.OffsetMinimal / 100, _sideDefault);

            var takeProfitPrice = CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, _sideDefault);
            var stopLossPrice = price - stopLoss;
            takeProfitPrice = Math.Round(takeProfitPrice, 2);
            stopLossPrice = Math.Round(stopLossPrice, 2);
            var volume = (decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
            var qty = GetQuantity(client, _symbolDefault, volume);
            var response = (stopLossPrice, takeProfitPrice, qty);
            return response;
        }
        private static decimal GetQuantity(BingXRestClient client, string symbol, decimal volume)
        {
            try
            {
                int round = GetDecimalPlaces(GetSymbolInfoAsync(client, symbol));

                var tickerResult = client.PerpetualFuturesApi.ExchangeData.GetTickerAsync(symbol).Result;

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
        private static decimal GetSymbolInfoAsync(BingXRestClient client, string symbol)
        {

            // Получаем информацию о паре
            var symbolInfoResult = client.PerpetualFuturesApi.ExchangeData.GetContractsAsync().Result;
            if (!symbolInfoResult.Success)
            {
                Console.WriteLine($"Ошибка: {symbolInfoResult.Error}");
                return 0;
            }

            var btcusdtInfo = symbolInfoResult.Data.FirstOrDefault(x => x.Symbol == symbol);
            if (btcusdtInfo != null)
            {
                return btcusdtInfo.MinOrderQuantity;
            }
            else
            {
                Console.WriteLine("Пара BTCUSDT не найдена");
            }
            return 0;
        }
        private static void Buy(AppUser user, KlineInterval interval, Config config)
        {
            try
            {
                var client = GetClient(user);
                if (client == null) return;

                var price = GetLastPrice(client, _symbolDefault);

                if (price > 0)
                {
                    var calculated = Calculated(client, interval, config, price);
                    
                    var buyOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                        _symbolDefault,
                        OrderSide.Buy,
                        FuturesOrderType.Market,
                        PositionSide.Long,
                        calculated.quantity
                        ).Result;

                    if (buyOrder.Success)
                    {
                        // Take Profit (тейк-профит) - рыночный
                        var takeProfitOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                            _symbolDefault,
                            OrderSide.Sell,
                            FuturesOrderType.StopMarket,
                            PositionSide.Long,
                            calculated.quantity,
                            stopPrice: calculated.stopLossPrice
                            ).Result;

                        if (takeProfitOrder.Success)
                        {
                            // Stop Loss(стоп-лосс) - рыночный
                            var stopLossOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                                _symbolDefault,
                                OrderSide.Sell,
                                FuturesOrderType.TakeProfitMarket,
                                PositionSide.Long,
                                calculated.quantity,
                                stopPrice: calculated.takeProfitPrice
                                ).Result;

                            if (stopLossOrder.Success)
                            {
                                var e = new Exception($"Сделка открыта!\n\n {DateTime.Now:s}");
                                TBot.LogError(e, user);
                            }
                            else
                            {
                                var e = new Exception(stopLossOrder.Error?.Message);
                                TBot.LogError(e, user);
                                Error.Log(e);
                            }
                        }
                        else
                        {
                            var e = new Exception(takeProfitOrder.Error?.Message);
                            TBot.LogError(e, user);
                            Error.Log(e);
                        }
                    }
                    else
                    {
                        var e = new Exception(buyOrder.Error?.Message);
                        TBot.LogError(e, user);
                        Error.Log(e);
                    }
                }
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }

        public static decimal GetBalance(string api, string secret)
        {
            var client = new BingXRestClient();
            client.SetApiCredentials(new ApiCredentials(api, secret));
            var result = client.PerpetualFuturesApi.Account.GetBalancesAsync().Result;
            if (result.Success)
            {
                var balance= result.Data.First().Balance;
                return balance;
            }

            return -1;
        }

        public static async Task<List<BingXPosition>?> GetPositions(string api, string secret, string symbol = "BTC-USDT")
        {
            return await Task.Run(() =>
            {
                try
                {


                    var client = new BingXRestClient();
                    client.SetApiCredentials(new ApiCredentials(api, secret));
                    var result = client.PerpetualFuturesApi.Trading.GetPositionsAsync(symbol).Result;
                    if (result.Success)
                    {
                        var balance = result.Data.ToList();
                        return balance;
                    }

                    return null;

                }
                catch (Exception e)
                {
                    Error.Log(e);
                    return null;
                }
            });
            
        }
    }
}
