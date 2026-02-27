using Newtonsoft.Json;

namespace Vanjaro.AI.Models
{
    public class HealthCheckResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("dnnVersion")]
        public string DnnVersion { get; set; }

        [JsonProperty("vanjaroVersion")]
        public string VanjaroVersion { get; set; }

        [JsonProperty("userId")]
        public int UserId { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("portalId")]
        public int PortalId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }
}
