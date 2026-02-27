using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class PageListItem
    {
        [JsonProperty("tabId")]
        public int TabId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("isVisible")]
        public bool IsVisible { get; set; }

        [JsonProperty("isDeleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty("hasVanjaroContent")]
        public bool HasVanjaroContent { get; set; }

        [JsonProperty("isPublished")]
        public bool IsPublished { get; set; }
    }

    public class PageListResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("skip")]
        public int Skip { get; set; }

        [JsonProperty("take")]
        public int Take { get; set; }

        [JsonProperty("pages")]
        public PageListItem[] Pages { get; set; }
    }
}
