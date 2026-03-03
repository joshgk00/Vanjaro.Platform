using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vanjaro.AI.Models
{
    public class BlockDetailResponse
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("componentId")]
        public string ComponentId { get; set; }

        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("blockTypeGuid")]
        public string BlockTypeGuid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("contentJSON")]
        public JToken ContentJSON { get; set; }

        [JsonProperty("styleJSON")]
        public JToken StyleJSON { get; set; }
    }
}
