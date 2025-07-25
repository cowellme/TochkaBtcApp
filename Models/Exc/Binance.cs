﻿using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using Newtonsoft.Json;
using System.IO;
using TochkaBtcApp.Contollers;

namespace TochkaBtcApp.Models.Exc;

public class Binance 
{
    private static string _exchangeName = "binance";
    private static string _sideDefault = "LONG";
    private static string _symbolDefault = "BTCUSDT";
    private static List<Order> _orders = new List<Order>();
    private static List<AppUser> _users = new List<AppUser>();

    public static async Task Checker()
    {
        await Task.Run(() =>
        {
            _orders = Order.GetOrders(_exchangeName);
            _users = ApplicationContext.GetUsers();
            while (true)
            {
                Thread.Sleep(1000);

                if (_orders.Count < 1) continue;

                foreach (var order in _orders.ToList())
                {
                    if (order.CheckPair()) _orders.Remove(order);
                }
            }
        });
    }
    private static BinanceRestClient? GetClient(AppUser user)
    {
        try
        {
            var newClient = new BinanceRestClient();
            
            newClient.SetApiCredentials(new ApiCredentials(user.ApiBinance, user.SecretBinance));

            return newClient;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
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
                var name = config.Name;
                var user = users.FirstOrDefault(x => x.Name == name);

                if (user != null)
                {
                    var api = user.ApiBinance;
                    var secret = user.SecretBinance;

                    if (string.IsNullOrEmpty(api) && string.IsNullOrEmpty(secret)) continue;

                    var interval = ConvertToLocalKline(globalInterval); 
                   // Buy(user, interval, config);
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
    private static void Buy(AppUser user, KlineInterval interval, Config config)
    {
        try
        {
            var client = GetClient(user);
            if (client == null) return;

            var price = GetLastPrice(client, _symbolDefault);

            if (price > 0)
            {
                //if LONG
                var stopLoss = CalculateStopLoss(client, interval, price, config.CandlesCount, (decimal)config.OffsetMinimal / 100, _sideDefault);

                var takeProfitPrice = CalculateTakeProfit(price, stopLoss, (decimal)config.RiskRatio, _sideDefault);
                var stopLossPrice = price - stopLoss;
                takeProfitPrice = Math.Round(takeProfitPrice, 2);
                stopLossPrice = Math.Round(stopLossPrice, 2);
                var volume = (decimal)config.Risk / (Math.Abs(stopLossPrice - price) / price);
                var qty = GetQuantity(client, _symbolDefault, volume);

                Error.Log(new Exception($"GetSignal SL:{stopLossPrice}, TP:{takeProfitPrice}, V:{volume}"));

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
                        stopPrice: takeProfitPrice
                    ).Result;

                    if (takeProfitOrder.Success)
                    {
                        // Stop Loss(стоп-лосс) - рыночный
                        var stopLossOrder = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol: _symbolDefault,
                            side: OrderSide.Sell,
                            type: FuturesOrderType.StopMarket,
                            quantity: qty,
                            stopPrice: stopLossPrice
                        ).Result;

                        if (stopLossOrder.Success)
                        {
                            _orders.Add(new Order
                            {
                                Api = user.ApiBinance,
                                Secret = user.SecretBinance,
                                Symbol = _symbolDefault,
                                TakeId = takeProfitOrder.Data.Id,
                                StopId = stopLossOrder.Data.Id,
                                StopPrice = stopLossPrice,
                                TakePrice = takeProfitPrice,
                                Name = config.Name,
                                Exchange = _exchangeName
                            });
                            Order.SaveOrders(_orders, _exchangeName);
                        }
                        else
                        {
                            Error.Log(new Exception(stopLossOrder.Error?.Message));
                        }
                    }
                    else
                    {
                        Error.Log(new Exception(takeProfitOrder.Error?.Message));
                    }
                }
                else
                {
                    Error.Log(new Exception(buyOrder.Error?.Message));
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
    private static decimal GetQuantity(BinanceRestClient client, string symbol, decimal volume)
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

        var max = listCandles?.MaxBy(x => x.HighPrice)?.HighPrice;

        if (max != null) return (decimal)max;

        return response;
    }
    private static decimal GetMinimalPriceForCandles(BinanceRestClient client, KlineInterval interval, int candlesCount)
    {
        var response = .0m;

        var listCandles = GetLastCandles(client, _symbolDefault, interval, candlesCount);

        var min = listCandles?.MinBy(x => x.LowPrice)?.LowPrice;

        if (min != null) return (decimal)min;

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
    public static List<BinanceFuturesUsdtTrade>? GetPositionsHistory(AppUser user)
    {
        try
        {
            return null;
            var client = GetClient(user);

            if (client != null)
            {
                var result = client.UsdFuturesApi.Trading.GetUserTradesAsync(_symbolDefault).Result;
                if (result.Success)
                {
                    var positions = result.Data.OrderByDescending(x => x.Timestamp).ToList();
                    return positions;
                }
            }
            
            return null;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
}

public class Order
{
    public string Symbol { get; set; }
    public string Api { get; set; }
    public string Secret { get; set; }
    public string Name { get; set; }
    public decimal TakePrice { get; set; }
    public decimal StopPrice { get; set; }
    public long TakeId { get; set; }
    public long StopId { get; set; }
    public string Exchange { get; set; }

    public static void SaveOrders(List<Order> orders, string exchange)
    {
        var path = Environment.CurrentDirectory + $@"\orders_{exchange}.json";
        var jsonString = JsonConvert.SerializeObject(orders);
        File.WriteAllText(path, jsonString);
    }

    public static List<Order> GetOrders(string exchange)
    {
        try
        {
            var path = Environment.CurrentDirectory + $@"\orders_{exchange}.json";
            var jsonString = File.ReadAllText(path);
            var orders = JsonConvert.DeserializeObject<List<Order>>(jsonString);

            if (orders == null)
            {
                orders = new List<Order>();
                SaveOrders(orders, exchange);
            }

            return orders;
        }
        catch (Exception e)
        {
            var orders = new List<Order>();
            SaveOrders(orders, exchange);
            return orders;
        }
    }
    public static bool CancelOrder(BinanceRestClient client ,string symbol ,long id)
    {
        var result = client.UsdFuturesApi.Trading.CancelOrderAsync(symbol, id).Result;

        if (result.Success)
        {
            return true;
        }

        return false;
    }
    public bool CancelSl(BinanceRestClient client)
    {
        try
        {
            var result = client.UsdFuturesApi.Trading.CancelOrderAsync(Symbol, StopId).Result;

            if (result.Success)
            {
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    public bool CancelTp(BinanceRestClient client)
    {
        try
        {
            var result = client.UsdFuturesApi.Trading.CancelOrderAsync(Symbol, TakeId).Result;

            if (result.Success)
            {
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    public bool CheckPair()
    {
        try
        {
            var user = ApplicationContext.GetUsers()?.FirstOrDefault(x => x.Name == Name);

            if (user == null) return false;

            var client = GetClient(user);

            if (client == null) return false;

            var result = client.UsdFuturesApi.Trading.GetOpenOrdersAsync(Symbol).Result;

            if (result.Success)
            {
                var openOrders = result.Data.ToList();

                var isOpenSl = false;
                var isOpenTp = false;

                if (openOrders.Count > 0)
                {
                    foreach (var order in openOrders)
                    {
                        if (order.Id == TakeId) isOpenTp = true;
                        if (order.Id == StopId) isOpenSl = true;
                    }

                    if (!isOpenTp) return CancelSl(client);
                    if (!isOpenSl) return CancelTp(client);
                }
            }

            return false;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    public static void ClearOrders(BinanceRestClient client, string symbol)
    {
        var result = client.UsdFuturesApi.Trading.GetOpenOrdersAsync(symbol).Result;

        if (result.Success)
        {
            var openOrders = result.Data.ToList();

            foreach (var order in openOrders)
            {
                var id = order.Id;
                CancelOrder(client, symbol, id);
            }
        }
    }
    private static BinanceRestClient? GetClient(AppUser user)
    {
        try
        {
            var newClient = new BinanceRestClient();

            newClient.SetApiCredentials(new ApiCredentials(user.ApiBinance, user.SecretBinance));

            return newClient;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
}