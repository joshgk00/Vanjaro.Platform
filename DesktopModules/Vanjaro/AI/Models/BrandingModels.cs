using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class BrandingResponse
    {
        [JsonProperty("siteName")]
        public string SiteName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("keywords")]
        public string Keywords { get; set; }

        [JsonProperty("footerText")]
        public string FooterText { get; set; }

        [JsonProperty("logo", NullValueHandling = NullValueHandling.Ignore)]
        public AssetFileItem Logo { get; set; }

        [JsonProperty("favicon", NullValueHandling = NullValueHandling.Ignore)]
        public AssetFileItem Favicon { get; set; }
    }

    public class BrandingUpdateRequest
    {
        [JsonProperty("siteName")]
        public string SiteName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("keywords")]
        public string Keywords { get; set; }

        [JsonProperty("footerText")]
        public string FooterText { get; set; }

        [JsonProperty("logoFileId")]
        public int? LogoFileId { get; set; }

        [JsonProperty("faviconFileId")]
        public int? FaviconFileId { get; set; }
    }
}
