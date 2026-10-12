using System.Text.Json;
using DotNetHistory.Models;
using Microsoft.AspNetCore.Hosting;

namespace DotNetHistory.Services;

public class MockFeatureService : IFeatureService
{
    private readonly IWebHostEnvironment _env;

    public MockFeatureService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<List<DotNetVersion>> GetCatalogAsync()
    {
        var filePath = Path.Combine(_env.WebRootPath, "data", "features.json");
        if (!File.Exists(filePath)) return new List<DotNetVersion>();

        try
        {
            await using var stream = File.OpenRead(filePath);
            var catalog = await JsonSerializer.DeserializeAsync<List<DotNetVersion>>(stream, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (catalog == null) return new List<DotNetVersion>();

            foreach (var version in catalog)
            {
                foreach (var feature in version.Features)
                {
                    if (FeatureRegistry.HasRunner(feature.Id))
                    {
                        string className = feature.Id switch
                        {
                            "core2-span" => "SpanMemoryExample",
                            _ => ""
                        };

                        if (!string.IsNullOrEmpty(className))
                        {
                            var sourcePath = Path.Combine(AppContext.BaseDirectory, "FeatureExamples", $"{className}.cs");
                            if (File.Exists(sourcePath))
                            {
                                feature.CodeExample = await File.ReadAllTextAsync(sourcePath);
                            }
                        }
                    }
                }
            }

            return catalog;
        }
        catch
        {
            return new List<DotNetVersion>();
        }
    }

    public string ExecuteFeatureCode(string featureId)
    {
        return FeatureRegistry.Run(featureId);
    }
}