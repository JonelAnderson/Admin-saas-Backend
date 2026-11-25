
using DS.Platform.Application.Interfaces.Serialization.Settings;
using Newtonsoft.Json;

namespace DS.Platform.Application.Serialization.Settings
{
    public class NewtonsoftJsonSettings : IJsonSerializerSettings
    {
        public JsonSerializerSettings JsonSerializerSettings { get; } = new();
    }
}