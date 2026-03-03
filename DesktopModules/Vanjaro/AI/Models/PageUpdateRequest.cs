using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class PageUpdateRequest
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }

        [JsonProperty("contentJSON")]
        public string ContentJSON { get; set; }

        [JsonProperty("styleJSON")]
        public string StyleJSON { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("expectedVersion")]
        public int? ExpectedVersion { get; set; }
    }
}
