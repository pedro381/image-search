using System.Text.Json.Serialization;

namespace ImageSearch.Model
{
    public class ImageData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("image")]
        public byte[] Image { get; set; } = Array.Empty<byte>();
    }
}
