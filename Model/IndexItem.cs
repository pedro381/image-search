using System.Text.Json.Serialization;

namespace ImageSearch.Model
{
    public class IndexItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
