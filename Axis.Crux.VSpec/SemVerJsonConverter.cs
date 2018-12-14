using System;
using Newtonsoft.Json;

namespace Axis.Crux.VSpec
{
    public class SemVerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(SemVerRange);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new SemVerRange(reader.Value?.ToString());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue($@"""{value.ToString()}""");
        }
    }
}
