using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class DesignControl
    {
        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("lessVariable")]
        public string LessVariable { get; set; }

        [JsonProperty("currentValue")]
        public string CurrentValue { get; set; }

        [JsonProperty("defaultValue")]
        public string DefaultValue { get; set; }

        [JsonProperty("defaultIsVariable")]
        public bool DefaultIsVariable { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("categoryGuid")]
        public string CategoryGuid { get; set; }

        [JsonProperty("rangeMin", NullValueHandling = NullValueHandling.Ignore)]
        public float? RangeMin { get; set; }

        [JsonProperty("rangeMax", NullValueHandling = NullValueHandling.Ignore)]
        public float? RangeMax { get; set; }

        [JsonProperty("increment", NullValueHandling = NullValueHandling.Ignore)]
        public float? Increment { get; set; }

        [JsonProperty("suffix", NullValueHandling = NullValueHandling.Ignore)]
        public string Suffix { get; set; }

        [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)]
        public DesignDropdownOption[] Options { get; set; }
    }

    public class DesignDropdownOption
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }
    }
}
