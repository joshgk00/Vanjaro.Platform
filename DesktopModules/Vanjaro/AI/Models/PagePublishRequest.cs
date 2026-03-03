using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class PagePublishRequest
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }

        [JsonProperty("version")]
        public int? Version { get; set; }
    }
}
