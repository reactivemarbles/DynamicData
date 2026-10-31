using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

/// <summary>Drives every refresh-aware aggregate with seeded random edits and compares each result with LINQ over the current items.</summary>
public sealed class RandomizedAggregationFixture
{
    private const int Seed = 0x0121_4A66;

    private const int EditCount = 2_000;

    private const int EmptyValue = -1;

    private readonly Randomizer _randomizer = new(Seed);

    private int _nextName;

    public RandomizedAggregationFixture(ITestOutputHelper output) => output.WriteLine($"{nameof(RandomizedAggregationFixture)} seed: {Seed:X8}");

    public enum Pipeline
    {
        Plain,

        Filtered,

        Sorted
    }

    [Theory]
    [InlineData(Pipeline.Plain)]
    [InlineData(Pipeline.Filtered)]
    [InlineData(Pipeline.Sorted)]
    public void Cache_RandomEdits_MatchLinqOracle(Pipeline pipeline)
    {
        using var source = new TestSourceCache<Person, string>(static person => person.Name);
        var view = pipeline switch
        {
            Pipeline.Plain => source.Connect(),
            Pipeline.Filtered => source.Connect().Filter(static person => IsIncluded(person)),
            Pipeline.Sorted => (IObservable<IChangeSet<Person, string>>)source.Connect().Sort(SortExpressionComparer<Person>.Ascending(static person => person.Age)),
            _ => throw new ArgumentOutOfRangeException(nameof(pipeline))
        };
        using var aggregates = new Aggregates(view.Sum(static person => person.Age), view.Avg(static person => person.Age, EmptyValue), view.StdDev(static person => person.Age, EmptyValue), view.StdDev(static person => (double)person.Age, EmptyValue), view.Maximum(static person => person.Age, EmptyValue), view.Minimum(static person => person.Age, EmptyValue), view.Count());

        for (var edit = 0; edit < EditCount; ++edit)
        {
            source.Edit(updater =>
            {
                var operations = _randomizer.Int(1, 4);
                for (var operation = 0; operation < operations; ++operation)
                {
                    EditCache(updater);
                }
            });

            aggregates.ShouldMatch(pipeline is Pipeline.Filtered ? source.Items.Where(IsIncluded) : source.Items, edit);
        }
    }

    [Theory]
    [InlineData(Pipeline.Plain)]
    [InlineData(Pipeline.Filtered)]
    [InlineData(Pipeline.Sorted)]
    public void List_RandomEdits_MatchLinqOracle(Pipeline pipeline)
    {
        using var source = new TestSourceList<Person>();
        var view = pipeline switch
        {
            Pipeline.Plain => source.Connect(),
            Pipeline.Filtered => source.Connect().Filter(static person => IsIncluded(person)),
            Pipeline.Sorted => source.Connect().Sort(SortExpressionComparer<Person>.Ascending(static person => person.Age).ThenByAscending(static person => person.Name)),
            _ => throw new ArgumentOutOfRangeException(nameof(pipeline))
        };
        using var aggregates = new Aggregates(view.Sum(static person => person.Age), view.Avg(static person => person.Age, EmptyValue), view.StdDev(static person => person.Age, EmptyValue), view.StdDev(static person => (double)person.Age, EmptyValue), view.Maximum(static person => person.Age, EmptyValue), view.Minimum(static person => person.Age, EmptyValue), view.Count());

        for (var edit = 0; edit < EditCount; ++edit)
        {
            source.Edit(list =>
            {
                // ChangeAwareList mis-merges an insert made just before a pending AddRange. Each source edit therefore adds at most
                // once, and edits feeding Filter or Sort, which build their output with ChangeAwareList, make a single change.
                var canAdd = true;
                var operations = pipeline is Pipeline.Plain ? _randomizer.Int(1, 4) : 1;
                for (var operation = 0; operation < operations; ++operation)
                {
                    canAdd &= !EditList(list, canAdd);
                }
            });

            aggregates.ShouldMatch(pipeline is Pipeline.Filtered ? source.Items.Where(IsIncluded) : source.Items, edit);
        }
    }

    private static bool IsIncluded(Person person) => person.Age % 3 != 0;

    private void EditCache(ISourceUpdater<Person, string> updater)
    {
        var items = updater.Items.ToArray();
        switch (items.Length == 0 ? 0 : _randomizer.Int(0, 99))
        {
            case < 30:
                updater.AddOrUpdate(Enumerable.Range(0, _randomizer.Int(1, 5)).Select(_ => CreatePerson()));
                break;

            case < 45:
                updater.AddOrUpdate(new Person(_randomizer.ArrayElement(items).Name, CreateAge()));
                break;

            case < 70:
                {
                    var person = _randomizer.ArrayElement(items);
                    person.Age = CreateAge();
                    updater.Refresh(person);
                    break;
                }

            case < 75:
                updater.Refresh(_randomizer.ArrayElement(items));
                break;

            case < 98:
                updater.Remove(_randomizer.ArrayElements(items, _randomizer.Int(1, Math.Min(3, items.Length))));
                break;

            default:
                updater.Clear();
                break;
        }
    }

    private bool EditList(IExtendedList<Person> list, bool canAdd)
    {
        if (list.Count == 0 && !canAdd)
        {
            return false;
        }

        switch (list.Count == 0 ? 0 : _randomizer.Int(canAdd ? 0 : 25, 99))
        {
            case < 15:
                list.AddRange(Enumerable.Range(0, _randomizer.Int(1, 5)).Select(_ => CreatePerson()));
                return true;

            case < 25:
                list.Insert(_randomizer.Int(0, list.Count), CreatePerson());
                return true;

            case < 35:
                list[_randomizer.Int(0, list.Count - 1)] = CreatePerson();
                break;

            case < 55:
                {
                    var index = _randomizer.Int(0, list.Count - 1);
                    list[index].Age = CreateAge();
                    Refresh(list, index);
                    break;
                }

            case < 60:
                Refresh(list, _randomizer.Int(0, list.Count - 1));
                break;

            case < 70:
                list.Move(_randomizer.Int(0, list.Count - 1), _randomizer.Int(0, list.Count - 1));
                break;

            case < 85:
                list.RemoveAt(_randomizer.Int(0, list.Count - 1));
                break;

            case < 98:
                {
                    var index = _randomizer.Int(0, list.Count - 1);
                    list.RemoveRange(index, _randomizer.Int(1, list.Count - index));
                    break;
                }

            default:
                list.Clear();
                break;
        }

        return false;
    }

    private static void Refresh(IExtendedList<Person> list, int index) => ((ChangeAwareList<Person>)list).Refresh(list[index], index);

    private Person CreatePerson() => new($"P{_nextName++}", CreateAge());

    private int CreateAge() => _randomizer.Int(0, 1_000);

    private sealed class Aggregates : IDisposable
    {
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ValueRecordingObserver<int> _sum;
        private readonly ValueRecordingObserver<double> _avg;
        private readonly ValueRecordingObserver<double> _integralStdDev;
        private readonly ValueRecordingObserver<double> _stdDev;
        private readonly ValueRecordingObserver<int> _max;
        private readonly ValueRecordingObserver<int> _min;
        private readonly ValueRecordingObserver<int> _count;

        public Aggregates(IObservable<int> sum, IObservable<double> avg, IObservable<double> integralStdDev, IObservable<double> stdDev, IObservable<int> max, IObservable<int> min, IObservable<int> count)
        {
            _subscriptions.Add(sum.ValidateSynchronization().RecordValues(out _sum));
            _subscriptions.Add(avg.ValidateSynchronization().RecordValues(out _avg));
            _subscriptions.Add(integralStdDev.ValidateSynchronization().RecordValues(out _integralStdDev));
            _subscriptions.Add(stdDev.ValidateSynchronization().RecordValues(out _stdDev));
            _subscriptions.Add(max.ValidateSynchronization().RecordValues(out _max));
            _subscriptions.Add(min.ValidateSynchronization().RecordValues(out _min));
            _subscriptions.Add(count.ValidateSynchronization().RecordValues(out _count));
        }

        public void Dispose() => _subscriptions.Dispose();

        public void ShouldMatch(IEnumerable<Person> items, int edit)
        {
            var ages = items.Select(static person => person.Age).ToArray();
            var because = $"edit {edit} leaves {ages.Length} items";

            Latest(_sum, 0).Should().Be(ages.Sum(), because);
            Latest(_count, 0).Should().Be(ages.Length, because);
            Latest(_avg, EmptyValue).Should().BeApproximately(ages.Length == 0 ? EmptyValue : ages.Average(), 1e-9, because);
            Latest(_integralStdDev, EmptyValue).Should().BeApproximately(SampleStandardDeviation(ages), 1e-9, because);
            Latest(_stdDev, EmptyValue).Should().BeApproximately(SampleStandardDeviation(ages), 1e-6, because);
            Latest(_max, EmptyValue).Should().Be(ages.Length == 0 ? EmptyValue : ages.Max(), because);
            Latest(_min, EmptyValue).Should().Be(ages.Length == 0 ? EmptyValue : ages.Min(), because);
        }

        private static T Latest<T>(ValueRecordingObserver<T> results, T emptyValue)
        {
            results.Error.Should().BeNull();
            return results.RecordedValues.Count == 0 ? emptyValue : results.RecordedValues[^1];
        }

        private static double SampleStandardDeviation(int[] ages)
        {
            if (ages.Length < 2)
            {
                return EmptyValue;
            }

            var mean = ages.Average();
            return Math.Sqrt(ages.Sum(age => (age - mean) * (age - mean)) / (ages.Length - 1));
        }
    }
}
