using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class PageCreateRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parentId")]
        public int? ParentId { get; set; }

        [JsonProperty("isVisible")]
        public bool IsVisible { get; set; } = true;

        [JsonProperty("contentJSON")]
        public string ContentJSON { get; set; }

        [JsonProperty("styleJSON")]
        public string StyleJSON { get; set; }
    }
}
