using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class BlockListItem
    {
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

        [JsonProperty("childCount")]
        public int ChildCount { get; set; }
    }

    public class BlockListResponse
    {
        [JsonProperty("pageId")]
        public int PageId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("blocks")]
        public BlockListItem[] Blocks { get; set; }
    }
}
