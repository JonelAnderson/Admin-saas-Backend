using Newtonsoft.Json;

namespace DS.Platform.Application.Configurations
{
    public class SingleOrArrayConverter<T> : JsonConverter
    {
        // Constructor sin parámetros
        public SingleOrArrayConverter() { }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Si el valor es un array, lo deserializa como una lista
            if (reader.TokenType == JsonToken.StartArray)
            {
                return serializer.Deserialize<List<T>>(reader)!;
            }

            // Si no es un array, asume que es un solo objeto y lo encapsula en una lista
            var singleValue = serializer.Deserialize<T>(reader);
            return new List<T> { singleValue! };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = (List<T>)value;
            if (list.Count == 1)
            {
                // Si hay un solo elemento, lo serializa como un objeto
                serializer.Serialize(writer, list[0]);
            }
            else
            {
                // Si hay varios elementos, lo serializa como una lista
                serializer.Serialize(writer, list);
            }
        }
    }


}
