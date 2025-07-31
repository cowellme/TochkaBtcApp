public class Pair
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