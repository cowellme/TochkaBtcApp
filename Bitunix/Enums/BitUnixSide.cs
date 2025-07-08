using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BitUnixApi.Enums;

[JsonConverter(typeof(StringEnumConverter))]
public enum BitUnixSide
{
   
    [Description("BUY")]
    Buy,
    [Description("SELL")]
    Sell
}