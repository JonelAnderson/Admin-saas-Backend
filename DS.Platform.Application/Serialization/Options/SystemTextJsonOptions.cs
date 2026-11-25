using System.Text.Json;
using DS.Platform.Application.Interfaces.Serialization.Options;

namespace DS.Platform.Application.Serialization.Options
{
    public class SystemTextJsonOptions : IJsonSerializerOptions
    {
        public JsonSerializerOptions JsonSerializerOptions { get; } = new();
    }
}