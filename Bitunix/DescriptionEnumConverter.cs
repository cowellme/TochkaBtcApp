using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;

namespace BitUnixApi;

public class DescriptionEnumConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var field = value.GetType().GetField(value.ToString());
        var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
        writer.WriteValue(description ?? value.ToString());
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        string value = reader.Value.ToString();

        foreach (var field in objectType.GetFields())
        {
            if (field.GetCustomAttribute<DescriptionAttribute>()?.Description == value)
                return field.GetValue(null);
        }
        return Enum.Parse(objectType, value);
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType.IsEnum;
    }
}