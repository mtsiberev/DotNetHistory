using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

            string examplesFolder = Path.Combine(_env.ContentRootPath, "FeatureExamples");
            if (!Directory.Exists(examplesFolder))
            {
                examplesFolder = Path.Combine(AppContext.BaseDirectory, "FeatureExamples");
            }

            if (Directory.Exists(examplesFolder))
            {
                var csFiles = Directory.GetFiles(examplesFolder, "*.cs");

                foreach (var version in catalog)
                {
                    foreach (var feature in version.Features)
                    {
                        if (string.IsNullOrEmpty(feature.Id)) continue;

                        string cleanId = feature.Id.Replace("-", "");
                        var realSourcePath = csFiles.FirstOrDefault(path =>
                            Path.GetFileNameWithoutExtension(path)
                                .Contains(cleanId, StringComparison.OrdinalIgnoreCase));

                        if (realSourcePath != null && File.Exists(realSourcePath))
                        {                
                            feature.CodeExample = await File.ReadAllTextAsync(realSourcePath);
                        }
                    }
                }
            }

            return catalog;
        }
        catch (Exception)
        {
            return new List<DotNetVersion>();
        }
    }

    public string ExecuteFeatureCode(string featureId)
    {
        return string.Empty;
    }
}
