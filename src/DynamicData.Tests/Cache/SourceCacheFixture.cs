namespace DynamicData.Tests.Cache;

public static partial class SourceCacheFixture
{
    public record SomeObject(int Id, int Value);

    private sealed record TestItem(string Key, string Value);
}
