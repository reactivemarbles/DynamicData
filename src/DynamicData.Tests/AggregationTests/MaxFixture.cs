using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

public class MaxFixture : IDisposable
{
    private readonly Randomizer _randomizer = new(0x4D61_7846);

    private readonly SourceCache<Person, string> _source;

    public MaxFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void Cache_ItemIsRefreshedAboveMaximum_MaximumFollowsIt()
    {
        var (low, middle, high) = CreateAscendingAges();
        var raised = high + _randomizer.Int(1, 100);
        var person = new Person("A", low);
        _source.AddOrUpdate(new[] { person, new Person("B", middle), new Person("C", high) });
        using var subscription = _source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        person.Age = raised;
        _source.Refresh(person);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, raised }, "refreshing an item above the maximum makes it the maximum");
    }

    [Fact]
    public void Cache_MaximumHolderIsRefreshedLower_MaximumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var holder = new Person("C", high);
        _source.AddOrUpdate(new[] { new Person("A", low), new Person("B", middle), holder });
        using var subscription = _source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        holder.Age = low - 1;
        _source.Refresh(holder);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, middle }, "the maximum is recomputed from the remaining values once its holder drops");
    }

    [Fact]
    public void Cache_MaximumHolderIsRefreshedUnchanged_MaximumIsNotEmittedAgain()
    {
        var (low, _, high) = CreateAscendingAges();
        var holder = new Person("B", high);
        _source.AddOrUpdate(new[] { new Person("A", low), holder });
        using var subscription = _source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        _source.Refresh(holder);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high }, "a refresh that does not change the value leaves the maximum where it was");
    }

    [Fact]
    public void Cache_MaximumHolderIsUpdatedLower_MaximumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        _source.AddOrUpdate(new[] { new Person("A", low), new Person("B", middle), new Person("C", high) });
        using var subscription = _source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        _source.AddOrUpdate(new Person("C", low));

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, middle }, "replacing the maximum holder with a lower value recomputes the maximum");
    }

    [Fact]
    public void Cache_MaximumHolderMutatedWithoutRefreshIsRemoved_StoredValuesAreUsed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var holder = new Person("C", high);
        var other = new Person("B", middle);
        _source.AddOrUpdate(new[] { new Person("A", low), other, holder });
        using var subscription = _source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        holder.Age = low;
        other.Age = high + 1;
        _source.Remove(holder);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, middle }, "without a refresh the operator keeps the last observed values");
    }

    [Fact]
    public void Cache_AllItemsAreRemoved_EmptyValueIsEmitted()
    {
        var (low, _, high) = CreateAscendingAges();
        var emptyValue = -_randomizer.Int(1, 100);
        _source.AddOrUpdate(new[] { new Person("A", low), new Person("B", high) });
        using var subscription = _source.Connect().Maximum(p => p.Age, emptyValue).ValidateSynchronization().RecordValues(out var results);

        _source.Clear();

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, emptyValue }, "an empty cache reports the configured empty value");
    }

    [Fact]
    public void List_MovedMaximumHolderIsRefreshedLower_MaximumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var holder = new Person("C", high);
        using var source = new TestSourceList<Person>();
        source.AddRange(new[] { new Person("A", low), new Person("B", middle), holder });
        using var subscription = source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        source.Move(2, 0);
        holder.Age = low - 1;
        source.Refresh(0);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, middle }, "the refresh addresses the holder at its moved index and the maximum is recomputed");
    }

    [Fact]
    public void List_MaximumHolderIsReplacedLower_MaximumIsRecomputed()
    {
        var (low, middle, high) = CreateAscendingAges();
        var holder = new Person("C", high);
        using var source = new TestSourceList<Person>();
        source.AddRange(new[] { new Person("A", low), new Person("B", middle), holder });
        using var subscription = source.Connect().Maximum(p => p.Age).ValidateSynchronization().RecordValues(out var results);

        source.Replace(holder, new Person("D", low));

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, middle }, "replacing the maximum holder with a lower value recomputes the maximum");
    }

    [Fact]
    public void List_ItemIsRefreshedAboveMaximumThenCleared_EmptyValueIsEmitted()
    {
        var (low, _, high) = CreateAscendingAges();
        var raised = high + _randomizer.Int(1, 100);
        var emptyValue = -_randomizer.Int(1, 100);
        var person = new Person("A", low);
        using var source = new TestSourceList<Person>();
        source.AddRange(new[] { person, new Person("B", high) });
        using var subscription = source.Connect().Maximum(p => p.Age, emptyValue).ValidateSynchronization().RecordValues(out var results);

        person.Age = raised;
        source.Refresh(0);
        source.Clear();

        results.Error.Should().BeNull();
        results.RecordedValues.Should().Equal(new[] { high, raised, emptyValue }, "the refresh raises the maximum and clearing the list empties it");
    }

    private (int Low, int Middle, int High) CreateAscendingAges()
    {
        var low = _randomizer.Int(1, 100);
        var middle = low + _randomizer.Int(1, 100);
        return (low, middle, middle + _randomizer.Int(1, 100));
    }

    [Fact]
    public void AddItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        result.Should().Be(30, "Max value should be 30");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        double max = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Maximum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => max = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);

        max.Should().Be(11, "Max should be 11");

        personc.Age = 100;

        max.Should().Be(100, "Max should be 100 after inline change");
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("C");
        result.Should().Be(20, "Max value should be 20 after remove");
        accumulator.Dispose();
    }
}
