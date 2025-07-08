using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BitUnixApi.Enums;

[JsonConverter(typeof(StringEnumConverter))]
public enum BitUnixTradeSide
{
    [Description("OPEN")]
    Open,
    [Description("SELL")]
    Close
}