namespace DynamicData.Benchmarks.Cache;

/// <summary>
/// Hot paths of the operators that keep one inner subscription per source item: an inner emission reaching
/// downstream, a property change becoming a refresh, and the cost of subscribing and unsubscribing inner
/// observables as items come and go.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class OrchestratedOperators : IDisposable
{
    private readonly SourceCache<Item, int> _source = new(item => item.Id);
    private Item[] _items = [];
    private ChangeSet<int, int>[] _childUpdates = [];
    private IDisposable? _subscription;
    private int _counter;

    [Params(1_000)]
    public int ItemCount { get; set; }

    [GlobalSetup(Target = nameof(TransformOnObservable_InnerNext))]
    public void SetupTransformOnObservable() => Setup(source => source.TransformOnObservable(item => item.Values).Subscribe());

    [GlobalSetup(Target = nameof(MergeManyChangeSets_InnerNext))]
    public void SetupMergeManyChangeSets() => Setup(source => source.MergeManyChangeSets(item => item.Children).Subscribe());

    [GlobalSetup(Target = nameof(MergeMany_InnerNext))]
    public void SetupMergeMany() => Setup(source => source.MergeMany(item => item.Values).Subscribe());

    [GlobalSetup(Target = nameof(AutoRefresh_PropertyChanged))]
    public void SetupAutoRefresh() => Setup(source => source.AutoRefresh(item => item.Value).Subscribe());

    [GlobalSetup(Target = nameof(TransformOnObservable_AddAndClear))]
    public void SetupAddAndClear()
    {
        CreateItems();
        _subscription = _source.Connect().TransformOnObservable(item => item.Values).Subscribe();
    }

    [Benchmark]
    public void TransformOnObservable_InnerNext() => EmitValues();

    [Benchmark]
    public void MergeManyChangeSets_InnerNext()
    {
        for (var i = 0; i < _items.Length; i++)
            _items[i].Children.OnNext(_childUpdates[i]);
    }

    [Benchmark]
    public void MergeMany_InnerNext() => EmitValues();

    [Benchmark]
    public void AutoRefresh_PropertyChanged()
    {
        foreach (var item in _items)
            item.Value = ++_counter;
    }

    [Benchmark]
    public void TransformOnObservable_AddAndClear()
    {
        _source.AddOrUpdate(_items);
        _source.Clear();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        _source.Clear();
    }

    private void Setup(Func<IObservable<IChangeSet<Item, int>>, IDisposable> subscribe)
    {
        CreateItems();
        _subscription = subscribe(_source.Connect());
        _source.AddOrUpdate(_items);

        foreach (var item in _items)
            item.Children.OnNext(new ChangeSet<int, int> { new(ChangeReason.Add, item.Id, item.Id) });
    }

    private void CreateItems()
    {
        _items = Enumerable.Range(1, ItemCount).Select(id => new Item(id)).ToArray();
        _childUpdates = _items.Select(item => new ChangeSet<int, int> { new(ChangeReason.Update, item.Id, item.Id, Optional.Some(item.Id)) }).ToArray();
    }

    private void EmitValues()
    {
        foreach (var item in _items)
            item.Values.OnNext(++_counter);
    }

    public sealed class Item(int id) : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; } = id;

        public Subject<int> Values { get; } = new();

        public BehaviorSubject<IChangeSet<int, int>> Children { get; } = new(ChangeSet<int, int>.Empty);

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}
