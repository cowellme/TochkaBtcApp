using BitUnixApi.Enums;
using Newtonsoft.Json;

namespace BitUnixApi;

public class BitUnixOrder
{
    public string symbol { get; set; }
    [JsonConverter(typeof(DescriptionEnumConverter))]
    public BitUnixSide side { get; set; }
    public string qty { get; set; }
    [JsonConverter(typeof(DescriptionEnumConverter))]
    public BitUnixTradeSide tradeSide { get; set; }
    [JsonConverter(typeof(DescriptionEnumConverter))]
    public BitUnixOrderType orderType { get; set; }
    public string? price { get; set; }
    public string? positionId { get; set; }
    public bool? reduceOnly { get; set; }
    public string? effect { get; set; }
    public string? clientId { get; set; }
    public string? tpPrice { get; set; }
    public string? slPrice { get; set; }
    public string? tpStopType { get; set; }
    public string? tpOrderType { get; set; }
    public string? tpOrderPrice { get; set; }
    public string? slStopType { get; set; }
    public string? slOrderType { get; set; }
    public string? slOrderPrice { get; set; }
}