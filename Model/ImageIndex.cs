using System.Text.Json.Serialization;

namespace ImageSearch.Model
{
    public class ImageIndex
    {
        [JsonPropertyName("items")]
        public List<IndexItem> Items { get; set; } = new();
    }
}
