using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class BlockUpdateRequest
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }

        [JsonProperty("componentId")]
        public string ComponentId { get; set; }

        [JsonProperty("contentJSON")]
        public string ContentJSON { get; set; }

        [JsonProperty("styleJSON")]
        public string StyleJSON { get; set; }

        [JsonProperty("expectedVersion")]
        public int? ExpectedVersion { get; set; }
    }
}
