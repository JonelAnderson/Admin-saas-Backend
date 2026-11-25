using System.Linq;
using DS.Platform.Shared.Constants.Localization;
using DS.Platform.Shared.Settings;

namespace DS.Platform.WebApi.Settings
{
    public record ServerPreference : IPreference
    {
        public string LanguageCode { get; set; } = LocalizationConstants.SupportedLanguages.FirstOrDefault()?.Code ?? "es-ES";

        //TODO - add server preferences
    }
}