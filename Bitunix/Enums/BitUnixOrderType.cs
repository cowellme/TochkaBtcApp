using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BitUnixApi.Enums;

[JsonConverter(typeof(StringEnumConverter))]
public enum BitUnixOrderType
{
    [Description("LIMIT")]
    Limit,
    [Description("MARKET")]
    Market

}