using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class DesignSettingsResponse
    {
        [JsonProperty("themeName")]
        public string ThemeName { get; set; }

        [JsonProperty("controls")]
        public DesignControl[] Controls { get; set; }

        [JsonProperty("availableFonts", NullValueHandling = NullValueHandling.Ignore)]
        public DesignFontOption[] AvailableFonts { get; set; }
    }

    public class DesignFontOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}
