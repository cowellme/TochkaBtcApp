using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Mvc;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects.Market;
using TochkaBtcApp.Contollers;
using TochkaBtcApp.Models;
using OrderSide = OKX.Net.Enums.OrderSide;

namespace TochkaBtcApp.Models.Exc
{
    public class OKX : IExchange
    {

        private static string _sideDefault = "LONG";
        private static string _symbolDefault = "BTC-USDT-SWAP";
        //private static KlineInterval _klineIntervalDefault = KlineInterval.FiveMinutes;
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
                    var name = config.Name;
                    var user = users.FirstOrDefault(x => x.Name == name);

                    if (user != null)
                    {
                        var api = user.ApiOKX;
                        var secret = user.SecretOKX;
                        var phrase = user.PhraseOKX;

                        if (string.IsNullOrEmpty(api) && string.IsNullOrEmpty(secret) && string.IsNullOrEmpty(phrase)) continue;

                        var client = new OKXRestClient();
                        client.SetApiCredentials(new ApiCredentials(api, secret, pass: phrase));
                        var interval = ConvertToLocalKline(globalInterval);
                        Buy(client, interval, config);
                    }
                }
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }
        private static KlineInterval ConvertToLocalKline(GlobalKlineInterval interval)
        {
            var allValues = Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().ToList();
            return allValues.FirstOrDefault(x => (int)x == (int)interval);
        }
        private static async void Buy(OKXRestClient client, KlineInterval interval, Config config)
        {
            try
            {
                var price = GetLastPrice(client, _symbolDefault);

                if (price > 0)
                {
                    //if LONG
                    var stopLoss = CalculateStopLoss(client, interval, price, config.CandlesCount, (decimal)config.OffsetMinimal / 100, _sideDefault);

                    var takeProfit = CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, _sideDefault);
                    var stopLossPrice = price - stopLoss;

                    takeProfit = Math.Round(takeProfit, 2);
                    stopLossPrice = Math.Round(stopLossPrice, 2);

                    var volume = 2359; //(decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
                    var qty = Math.Round(GetQuantityByBit(client, _symbolDefault, volume), 2);

                    //var resLever = client.UnifiedApi.Account.SetLeverageAsync(100, MarginMode.Cross, asset: "USDT")
                    //    .Result;

                    //if (resLever.Success)
                    //{

                    //}
                    //else
                    //{
                    //    Error.Log(new Exception(resLever.Error?.Message));
                    //}

                    Error.Log(new Exception($"GetSignal SL:{stopLossPrice}, TP:{takeProfit}, V:{volume}"));
                    var buyOrderResult = client.UnifiedApi.Trading.PlaceOrderAsync(
                        symbol: _symbolDefault,
                        tradeMode: TradeMode.Cross,
                        side: OrderSide.Buy,
                        type: OrderType.Market,
                        quantity: qty,
                        asset: "USDT"
                    ).Result;

                    if (buyOrderResult.Success)
                    {
                        var stopLossOrder = client.UnifiedApi.Trading.PlaceAlgoOrderAsync(
                            symbol: _symbolDefault,
                            TradeMode.Cross,
                            OrderSide.Sell,
                            AlgoOrderType.Conditional,
                            slTriggerPrice: stopLossPrice,
                            slTriggerPxType: AlgoPriceType.Last,
                            slOrderPrice: stopLossPrice,
                            quantity: qty,
                            asset: "USDT"
                        ).Result;

                        if (stopLossOrder.Success)
                        {
                            var takeProfitOrder = client.UnifiedApi.Trading.PlaceAlgoOrderAsync(
                                symbol: _symbolDefault,
                                TradeMode.Cross,
                                OrderSide.Sell,
                                AlgoOrderType.Conditional,
                                tpTriggerPrice: takeProfit,
                                tpTriggerPxType: AlgoPriceType.Last,
                                tpOrderPrice: takeProfit,
                                quantity: qty,
                                asset: "USDT"
                            ).Result;

                            if (takeProfitOrder.Success)
                            {

                            }
                            else
                            {
                                Error.Log(new Exception(takeProfitOrder.Error?.Message));
                            }
                        }
                        else
                        {
                            Error.Log(new Exception(stopLossOrder.Error?.Message));
                        }
                    }
                    else
                    {
                        Error.Log(new Exception($"{buyOrderResult.Error?.Message},Vol: {volume}"));
                    }
                }
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }
        private static decimal GetLastPrice(OKXRestClient client, string symbol)
        {
            try
            {
                var price = client.UnifiedApi.ExchangeData.GetTickerAsync(symbol).Result.Data.LastPrice ?? -1;
                return price;
            }
            catch (Exception e)
            {
                RLogger.Error(e);
                return -1;
            }
        }
        public static decimal GetSymbolInfoAsync(OKXRestClient client, string symbol)
        {

            // Получаем информацию о паре,
            var symbolInfoResult = client.UnifiedApi.Account.GetSymbolsAsync(InstrumentType.Swap).Result;


            if (!symbolInfoResult.Success)
            {
                Console.WriteLine($"Ошибка: {symbolInfoResult.Error}");
                return 0;
            }

            var btcusdtInfo = symbolInfoResult.Data.FirstOrDefault(x => x.Symbol == symbol);
            if (btcusdtInfo != null)
            {
                return btcusdtInfo.LotSize.Value;
            }
            else
            {
                Console.WriteLine("Пара BTCUSDT не найдена");
            }
            return 0;
        }
        private static decimal GetQuantityByBit(OKXRestClient client, string symbol, decimal volume)
        {
            try
            {
                int round = GetDecimalPlaces(GetSymbolInfoAsync(client, symbol));

                var tickerResult = client.UnifiedApi.ExchangeData.GetTickerAsync(symbol).Result;

                if (!tickerResult.Success) RLogger.Error(new Exception($"Ошибка при получении цены: {tickerResult.Error}"));

                var ticker = tickerResult.Data;

                decimal price = ticker.LastPrice ?? -1; // Текущая цена актива

                // var leverage = client.UsdFuturesApi.Account.GetSymbolConfigurationAsync().Result.Data.First().Leverage;
                // Рассчитываем количество криптовалюты
                decimal amount = volume / price;
                var response = Math.Round(amount, round);
                return response;


            }
            catch (Exception ex)
            {
                Error.Log(ex);
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
        private static decimal GetMaximumPriceForCandles(OKXRestClient client, KlineInterval interval, int candlesCount)
        {
            var response = .0m;

            var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

            var max = listCandles?.MaxBy(x => x.HighPrice)?.ClosePrice;

            if (max != null) return (decimal)max;

            return response;
        }
        private static decimal GetMinimalPriceForCandles(OKXRestClient client, KlineInterval interval, int candlesCount)
        {
            var response = .0m;

            var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

            var max = listCandles?.MinBy(x => x.LowPrice)?.ClosePrice;

            if (max != null) return (decimal)max;

            return response;
        }
        private static List<OKXKline>? GetLastCandles(OKXRestClient client, string symbol, KlineInterval interval, int candlesCount)
        {
            try
            {
                var starDateTime = DateTime.UtcNow.AddSeconds((int)interval * -candlesCount);
                var endDateTime = DateTime.UtcNow;
                var result = client.UnifiedApi.ExchangeData.GetKlinesAsync(symbol, interval, starDateTime, endDateTime).Result;
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
        private static decimal CalculateStopLoss(OKXRestClient client, KlineInterval interval, decimal openPrice, int candlesCount, decimal offsetMinimal, string side)
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
}
