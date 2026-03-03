using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class DesignUpdateRequest
    {
        [JsonProperty("controls")]
        public DesignUpdateItem[] Controls { get; set; }
    }

    public class DesignUpdateItem
    {
        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("lessVariable")]
        public string LessVariable { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}
