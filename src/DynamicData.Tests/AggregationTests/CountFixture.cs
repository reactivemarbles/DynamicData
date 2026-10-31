using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

public sealed class CountFixture
{
    private readonly Randomizer _randomizer = new(0x436F_756E);

    [Fact]
    public void Cache_ItemIsRefreshed_CountIsUnchanged()
    {
        var people = Enumerable.Range(0, _randomizer.Int(1, 10)).Select(index => new Person($"P{index}", _randomizer.Int(1, 100))).ToArray();
        using var source = new TestSourceCache<Person, string>(static person => person.Name);
        source.AddOrUpdate(people);
        using var counts = source.Connect().Count().ValidateSynchronization().RecordValues(out var countResults);
        using var isEmpty = source.Connect().IsEmpty().RecordValues(out var isEmptyResults);

        people[0].Age = _randomizer.Int(1, 100);
        source.Refresh(people[0]);

        countResults.Error.Should().BeNull();
        countResults.RecordedValues.Should().AllSatisfy(count => count.Should().Be(people.Length, "a refresh neither adds nor removes items"));
        isEmptyResults.RecordedValues.Should().EndWith(false, "a refresh does not empty the cache");
    }

    [Fact]
    public void List_ItemIsRefreshed_CountIsUnchanged()
    {
        var people = Enumerable.Range(0, _randomizer.Int(1, 10)).Select(index => new Person($"P{index}", _randomizer.Int(1, 100))).ToArray();
        using var source = new TestSourceList<Person>();
        source.AddRange(people);
        using var counts = source.Connect().Count().ValidateSynchronization().RecordValues(out var countResults);
        using var isNotEmpty = source.Connect().IsNotEmpty().RecordValues(out var isNotEmptyResults);

        people[0].Age = _randomizer.Int(1, 100);
        source.Refresh(0);

        countResults.Error.Should().BeNull();
        countResults.RecordedValues.Should().AllSatisfy(count => count.Should().Be(people.Length, "a refresh neither adds nor removes items"));
        isNotEmptyResults.RecordedValues.Should().EndWith(true, "a refresh does not empty the list");
    }
}
