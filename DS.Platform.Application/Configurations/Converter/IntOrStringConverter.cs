using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DS.Platform.Application.Configurations.Converter
{
    public class IntOrStringConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value))
            {
                return value;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();

                // Si es una cadena vacía, retorna null
                if (string.IsNullOrEmpty(stringValue))
                {
                    return null;
                }

                // Intenta convertir la cadena a int
                if (int.TryParse(stringValue, out value))
                {
                    return value;
                }
            }

            // Si no es un número válido, lanza una excepción
            throw new JsonException($"No se puede convertir el valor a int: {reader.GetString()}");
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
            }
            else
            {
                writer.WriteStringValue(string.Empty); // Escribe cadena vacía si el valor es null
            }
        }
    }

}
