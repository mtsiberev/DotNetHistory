using DotNetHistory.FeatureExamples;

namespace DotNetHistory.Services;

public static class FeatureRegistry
{
    private static readonly Dictionary<string, Func<IFeatureRunner>> Runners = new()
    {
        { "core2-span", () => new SpanMemoryExample() }
        // { "net8-primary", () => new PrimaryConstructorExample() }
    };

    public static bool HasRunner(string featureId) => Runners.ContainsKey(featureId);

    public static string Run(string featureId)
    {
        if (Runners.TryGetValue(featureId, out var factory))
        {
            try
            {
                var runner = factory();
                var result = runner.Execute();

                var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(standardOutput);

                return result;
            }
            catch (Exception ex)
            {
                return $"Ошибка выполнения: {ex.Message}";
            }
        }
        return "Пример кода не зарегистрирован.";
    }
}