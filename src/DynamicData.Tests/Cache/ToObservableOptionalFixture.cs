using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Tests.Cache;

public class ToObservableOptionalFixture : IDisposable
{
    private const string Key1 = "Key1";
    private const string Key2 = "Key2";
    private const string Value1 = "Value1";
    private const string Value2 = "Value2";
    private const string Value1AllCaps = "VALUE1";

    private readonly ISourceCache<KeyValuePair, string> _source = new SourceCache<KeyValuePair, string>(kvp => kvp.Key);
    private readonly ChangeSetAggregator<KeyValuePair, string> _results;

    public ToObservableOptionalFixture() => _results = _source.Connect().AsAggregator();

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task NullChecks() => await Assert.That(() => ObservableCacheEx.ToObservableOptional<KeyValuePair, string>(null!, string.Empty)).Throws<ArgumentNullException>();

    [Test]
    public async Task AddingToCacheEmitsOptionalSome()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key1, Value1));

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(1);
        await Assert.That(optionals[0].HasValue).IsTrue();
        await Assert.That(optionals[0].Value.Value).IsEqualTo(Value1);
    }

    [Test]
    public async Task AddingOtherKeysDoesNotEmit()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key2, Value1));

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExistingValueEmitsOptionalSome()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(1);
        await Assert.That(optionals[0].HasValue).IsTrue();
        await Assert.That(optionals[0].Value.Value).IsEqualTo(Value1);
    }

    [Test]
    public async Task RemovingFromCacheEmitsOptionalNone()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.RemoveKey(Key1);

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(0);
        await Assert.That(optionals.Count).IsEqualTo(2);
        await Assert.That(optionals[1].HasValue).IsFalse();
    }

    [Test]
    public async Task UpdateCacheEmitsOptionalSome()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.AddOrUpdate(Create(Key1, Value2));

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(2);
        await Assert.That(optionals[1].HasValue).IsTrue();
        await Assert.That(optionals[1].Value.Value).IsEqualTo(Value2);
    }

    [Test]
    public async Task UpdateUsesEqualityComparer()
    {
        // having
        var optionalsCS = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        var optionalsNonCS = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalCSObservable = _source.Connect().ToObservableOptional(Key1, CaseSensitiveComparer).Do(optionalsCS.Add).Subscribe();
        using var optionalNonCSObservable = _source.Connect().ToObservableOptional(Key1, CaseInsensitiveComparer).Do(optionalsNonCS.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.AddOrUpdate(Create(Key1, Value1AllCaps));

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionalsNonCS.Count).IsEqualTo(1);
        await Assert.That(optionalsNonCS[0].HasValue).IsTrue();
        await Assert.That(optionalsNonCS[0].Value.Value).IsEqualTo(Value1);
        await Assert.That(optionalsCS.Count).IsEqualTo(2);
        await Assert.That(optionalsCS[0].HasValue).IsTrue();
        await Assert.That(optionalsCS[0].Value.Value).IsEqualTo(Value1);
        await Assert.That(optionalsCS[1].HasValue).IsTrue();
        await Assert.That(optionalsCS[1].Value.Value).IsEqualTo(Value1AllCaps);
    }

    [Test]
    public async Task UpdateWhenReferenceEqualDoesNotEmit()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        var kvp = Create(Key1, Value1);
        _source.AddOrUpdate(kvp);

        // when
        _source.AddOrUpdate(kvp);
        _source.AddOrUpdate(kvp);
        _source.AddOrUpdate(kvp);

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(1);
        await Assert.That(optionals[0].HasValue).IsTrue();
        await Assert.That(optionals[0].Value.Value).IsEqualTo(Value1);
    }

    [Test]
    public async Task InitialOptionalAvoidsNoneAfterSomeRaceConditions()
    {
        await Task.WhenAll(Enumerable.Range(0, 10000).Select(_ => RunTest()));

        async Task RunTest()
        {
            // having
            using ISourceCache<KeyValuePair, string> source = new SourceCache<KeyValuePair, string>(kvp => kvp.Key);
            var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();

            // when
            var addTask = Task.Run(() => source.AddOrUpdate(Create(Key1, Value1)));
            using var optionalObservable = source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();
            await addTask;

            // then
            await Assert.That(source.Count).IsEqualTo(1);
            await Assert.That(optionals.Count >= 1 && optionals.Count <= 2).IsTrue();
            await Assert.That(optionals.Last().HasValue).IsTrue();
            await Assert.That(optionals.Last().Value.Value).IsEqualTo(Value1);
            if (optionals.Count > 1)
            {
                await Assert.That(optionals.First().HasValue).IsFalse();
            }
        }
    }

    [Test]
    public async Task InitialOptionalWhenMissingEmitsNone()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(0);
        await Assert.That(optionals.Count).IsEqualTo(1);
        await Assert.That(optionals[0].HasValue).IsFalse();
    }

    [Test]
    public async Task InitialOptionalWhenPresentEmitsSome()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(1);
        await Assert.That(optionals[0].HasValue).IsTrue();
        await Assert.That(optionals[0].Value.Value).IsEqualTo(Value1);
    }

    [Test]
    public async Task InitialOptionalWhenAddedEmitsNoneThenSome()
    {
        // having
        var optionals = new List<ReactiveUI.Primitives.Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key1, Value1));

        // then
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(optionals.Count).IsEqualTo(2);
        await Assert.That(optionals[0].HasValue).IsFalse();
        await Assert.That(optionals[1].HasValue).IsTrue();
        await Assert.That(optionals[1].Value.Value).IsEqualTo(Value1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ObservableCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
    {
        // having
        bool completed = false;
        var optionalObservable = _source.Connect();
        if (!completeSource)
        {
            optionalObservable = optionalObservable.Concat(Observable.Never<IChangeSet<KeyValuePair, string>>());
        }

        // when
        using var results = optionalObservable.ToObservableOptional(Key1).Subscribe(_ => { }, () => completed = true);
        _source.Dispose();

        // then
        await Assert.That(completed).IsEqualTo(completeSource);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ObservableFailsIfAndOnlyIfSourceFails(bool failSource)
    {
        // having
        var optionalObservable = _source.Connect();
        var testException = new Exception("Test");
        var receivedError = default(Exception);
        if (failSource)
        {
            optionalObservable = optionalObservable.Concat(Observable.Throw<IChangeSet<KeyValuePair, string>>(testException));
        }

        // when
        using var results = optionalObservable.ToObservableOptional(Key1).Subscribe(_ => { }, err => receivedError = err);
        _source.Dispose();

        // then
        await Assert.That(receivedError).IsEqualTo(failSource ? testException : default);
    }

    private static KeyValuePair Create(string key, string value) => new(key, value);

    private class KeyValueCompare(IEqualityComparer<string> stringComparer) : IEqualityComparer<KeyValuePair>
    {
        private IEqualityComparer<string> _stringComparer = stringComparer;

        public bool Equals([DisallowNull] KeyValuePair x, [DisallowNull] KeyValuePair y) => _stringComparer.Equals(x.Value, y.Value);
        [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Suppressed for Net 9.0")]
        public int GetHashCode([DisallowNull] KeyValuePair obj) => throw new NotImplementedException();
    }

    private static KeyValueCompare CaseInsensitiveComparer => new(StringComparer.OrdinalIgnoreCase);

    private static KeyValueCompare CaseSensitiveComparer => new(StringComparer.Ordinal);

    private class KeyValuePair(string key, string value)
    {
        public string Key { get; } = key;

        public string Value { get; } = value;
    }
}
