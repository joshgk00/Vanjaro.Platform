using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vanjaro.AI.Models
{
    public class PageDetailResponse
    {
        [JsonProperty("tabId")]
        public int TabId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("isPublished")]
        public bool IsPublished { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("createdOn")]
        public string CreatedOn { get; set; }

        [JsonProperty("updatedOn")]
        public string UpdatedOn { get; set; }

        [JsonProperty("contentJSON")]
        public JToken ContentJSON { get; set; }

        [JsonProperty("styleJSON")]
        public JToken StyleJSON { get; set; }

        [JsonProperty("contentHtml")]
        public string ContentHtml { get; set; }
    }
}
