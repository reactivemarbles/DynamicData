using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

public class MinFixture : IDisposable
{
    private readonly Randomizer _randomizer = new(0x4D69_6E46);

    private readonly SourceCache<Person, string> _source;

    public MinFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void Cache_ItemIsRefreshedBelowMinimum_MinimumFollowsIt()
    {
        var (low, middle, high) = CreateAscendingAges();
        var person = new Person("C", high);
        _source.AddOrUpdate(new[] { new Person("A", low), new Person("B", middle), person });
        using var subscription = _source.Connect().Minimum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        person.Age = low - 1;
        _source.Refresh(person);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { low, low - 1 }, "refreshing an item below the minimum makes it the minimum");
    }

    [Fact]
    public void Cache_MinimumHolderIsRefreshedHigher_MinimumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var holder = new Person("A", low);
        _source.AddOrUpdate(new[] { holder, new Person("B", middle), new Person("C", high) });
        using var subscription = _source.Connect().Minimum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        holder.Age = high + 1;
        _source.Refresh(holder);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { low, middle }, "the minimum is recomputed from the remaining values once its holder rises");
    }

    [Fact]
    public void List_MinimumHolderIsRefreshedHigherThenRemoved_MinimumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var emptyValue = -_randomizer.Int(1, 100);
        var holder = new Person("A", low);
        using var source = new TestSourceList<Person>();
        source.AddRange(new[] { holder, new Person("B", middle), new Person("C", high) });
        using var subscription = source.Connect().Minimum(p => p.Age, emptyValue).ValidateSynchronization().RecordValues(out var results);

        holder.Age = high + 1;
        source.Refresh(0);
        source.RemoveRange(1, 2);
        source.RemoveAt(0);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { low, middle, high + 1, emptyValue }, "each change recomputes the minimum from the stored values");
    }

    private (int Low, int Middle, int High) CreateAscendingAges()
    {
        var low = _randomizer.Int(1, 100);
        var middle = low + _randomizer.Int(1, 100);
        return (low, middle, middle + _randomizer.Int(1, 100));
    }

    [Fact]
    public void AddedItemsContributeToSum()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        result.Should().Be(10, "Min value should be 10");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        double min = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Minimum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => min = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);
        min.Should().Be(5);

        _source.AddOrUpdate(personc);

        personc.Age = 11;

        min.Should().Be(10);
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveProduceCorrectResult()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        result.Should().Be(20, "Min value should be 20 after remove");
        accumulator.Dispose();
    }
}
