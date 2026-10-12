using DotNetHistory.Models;

namespace DotNetHistory.Services;

public interface IFeatureService
{
    Task<List<DotNetVersion>> GetCatalogAsync();
    string ExecuteFeatureCode(string featureId);
}
