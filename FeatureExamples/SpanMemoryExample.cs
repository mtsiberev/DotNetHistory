using DotNetHistory.Services;

namespace DotNetHistory.FeatureExamples;

public class SpanMemoryExample : IFeatureRunner
{
    public string Execute()
    {
        using var writer = new StringWriter();
        Console.SetOut(writer);

        ReadOnlySpan<char> source = "2026-10-11".AsSpan();
        ReadOnlySpan<char> year = source.Slice(0, 4);

        Console.WriteLine($"Исходная строка: {source}");
        Console.WriteLine($"Вырезанный год через Span: {year}");

        return writer.ToString();
    }
}
