using System;

namespace DotNetHistory.FeatureExamples;

public class SpanMemoryExample
{
    public string Execute()
    {
        ReadOnlySpan<char> source = "2026-10-11".AsSpan();
        ReadOnlySpan<char> year = source.Slice(0, 4);

        Console.WriteLine($"Исходная строка: {source}");
        Console.WriteLine($"Вырезанный год через Span: {year}");

        return string.Empty;
    }
}
