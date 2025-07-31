using System.Diagnostics;
using BingX.Net.Clients;
using BingX.Net.Enums;
using BingX.Net.Objects.Models;
using BitUnixApi;
using BitUnixApi.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using Telegram.Bot.Types;
using TochkaBtcApp.Components;
using TochkaBtcApp.Contollers;
using TochkaBtcApp.Telegram;

namespace TochkaBtcApp.Models.Exc
{
    public class BingX : IExchange
    {
        private static string _exchangeName = "bingx";
        private static string _sideDefault = "LONG";
        private static string _symbolDefault = "BTC-USDT";
        private static List<Order> _orders = new List<Order>();
        private static List<AppUser> _users = new List<AppUser>();
        private IExchange _exchangeImplementation;

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
        private static async Task<decimal> GetLastPrice(BingXRestClient client, string symbol)
        {
            try
            {
                var response = await client.PerpetualFuturesApi.ExchangeData.GetTickerAsync(symbol);
                var price = response.Data.LastPrice;
                return price;
            }
            catch (Exception e)
            {
                RLogger.Error(e);
                return -1;
            }
        }
        private static decimal CalculateStopLoss(BingXRestClient client, KlineInterval interval, decimal openPrice,
            int candlesCount, decimal offsetMinimal, string side, string symbol)
        {
            var response = 0m;

            switch (side)
            {
                case "LONG":
                    var minPrice = GetMinimalPriceForCandles(client, interval, candlesCount, symbol);
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
        private static decimal GetMinimalPriceForCandles(BingXRestClient client, KlineInterval interval,
            int candlesCount, string symbol)
        {
            var response = .0m;

            var listCandles = GetLastCandles(client, symbol, interval, candlesCount);

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
        public static decimal CalculateTakeProfit(decimal openPrice, decimal stopLoss, decimal riskRatio, PositionSide side)
        {
            var response = .0m;

            switch (side)
            {
                case PositionSide.Long:
                    response = (openPrice - (openPrice - stopLoss)) * riskRatio + openPrice;
                    break;

                case PositionSide.Short:
                    response = (openPrice - (openPrice + stopLoss)) * riskRatio + openPrice;
                    break;
            }

            return response;
        }
        public async Task<string> GetSignal(GlobalKlineInterval globalInterval)
        {
            try
            {
                await Task.Run(() =>
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
                });
                return "";
            }
            catch (Exception e)
            {
                Error.Log(e);
                return e.Message;
            }
        }
        public async Task<List<Pair>?> GetAllPairs()
        {
            var client = new BingXRestClient();
            var response = await client.PerpetualFuturesApi.ExchangeData.GetTickersAsync();
            var pairs = new List<Pair>();
            var data = response.Data.ToList();
            foreach (var pair in data)
            {
                pairs.Add(new Pair
                {
                    symbol = pair.Symbol
                });
            }

            return pairs;
        }
        private static (decimal stopLossPrice, decimal takeProfitPrice, decimal quantity) Calculated(
            BingXRestClient client, KlineInterval interval, Config config, decimal price, PositionSide side = PositionSide.Long,
            string symbol = "BTC-USDT")
        {

            var stopLoss = CalculateStopLoss(client, interval, price, config.CandlesCount, (decimal)config.OffsetMinimal / 100, _sideDefault, symbol);

            var takeProfitPrice = CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, side);
            var stopLossPrice = price - stopLoss;
            takeProfitPrice = Math.Round(takeProfitPrice, 2);
            stopLossPrice = Math.Round(stopLossPrice, 2);
            var volume = (decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
            var qty = GetQuantity(client, symbol, volume);
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

                var price = GetLastPrice(client, _symbolDefault).Result;

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
        public async Task<string?> BuyHSignal(AppUser user, hSignal signal)
        {
            try
            {
                var config = signal.Config;
                config.TimeFrame = signal.TimeFrame;
                var order = await BuyH(user, signal);
                return order;
            }
            catch (Exception e)
            {
                Error.Log(e);
                return e.Message;
            }
        }
        private async Task<string?> BuyH(AppUser user, hSignal signal)
        {
            try
            {
                string symbol = signal.Symbol;
                var client = GetClient(user);
                if (client == null) throw new Exception("Error API");

                var price = await GetLastPrice(client, symbol);
                if (price < 0) throw new Exception("Price is not found");


                var side = signal.Side.ToLower() == "long" ? PositionSide.Long : PositionSide.Short;
                var unside = side == PositionSide.Long ? PositionSide.Short : PositionSide.Long;
                var config = signal.Config;
                var interval = ParseInterval(signal.TimeFrame);
                var calculated = Calculated(client, interval, config, price, side, symbol);
                


                var buyOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                    symbol,
                    OrderSide.Buy,
                    FuturesOrderType.Market,
                    side,
                    calculated.quantity
                ).Result;

                if (buyOrder.Success)
                {
                    
                    var takeProfitOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                        symbol,
                        OrderSide.Sell,
                        FuturesOrderType.StopMarket,
                        unside,
                        calculated.quantity,
                        stopPrice: calculated.stopLossPrice
                    ).Result;

                    if (takeProfitOrder.Success)
                    {
                        var stopLossOrder = client.PerpetualFuturesApi.Trading.PlaceOrderAsync(
                            symbol,
                            OrderSide.Sell,
                            FuturesOrderType.TakeProfitMarket,
                            unside,
                            calculated.quantity,
                            stopPrice: calculated.takeProfitPrice
                        ).Result;

                        if (stopLossOrder.Success)
                        {
                            user.SendAlert($"{side.ToString()} {symbol}\n" +
                                                 $"Take: $ {calculated.takeProfitPrice:0.00}\n" +
                                                 $"Open: $ {price:0.00}\n" +
                                                 $"Stop: $ {calculated.stopLossPrice:0.00}");
                            var e = new Exception($"Сделка открыта!\n\n {DateTime.Now:s}");
                            TBot.LogError(e, user);
                            return "success";
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


                return "";
            }
            catch (Exception e)
            {
                e.Source = "418:1 OrderByConfig";
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
    }
}
