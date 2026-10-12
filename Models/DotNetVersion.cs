namespace DotNetHistory.Models
{
    public class DotNetVersion
    {
        public string Name { get; set; } = string.Empty;
        public List<FeatureItem> Features { get; set; } = new();
    }
}
