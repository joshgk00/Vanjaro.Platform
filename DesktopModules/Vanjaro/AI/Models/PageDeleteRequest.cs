using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class PageDeleteRequest
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }
    }
}
