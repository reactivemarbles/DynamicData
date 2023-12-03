using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;

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

    [Fact]
    public void NullChecks() => Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.ToObservableOptional<KeyValuePair, string>(null!, string.Empty));

    [Fact]
    public void AddingToCacheEmitsOptionalSome()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key1, Value1));

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(1);
        optionals[0].HasValue.Should().BeTrue();
        optionals[0].Value.Value.Should().Be(Value1);
    }

    [Fact]
    public void AddingOtherKeysDoesNotEmit()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key2, Value1));

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(0);
    }

    [Fact]
    public void ExistingValueEmitsOptionalSome()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(1);
        optionals[0].HasValue.Should().BeTrue();
        optionals[0].Value.Value.Should().Be(Value1);
    }

    [Fact]
    public void RemovingFromCacheEmitsOptionalNone()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.RemoveKey(Key1);

        // then
        _results.Data.Count.Should().Be(0);
        optionals.Count.Should().Be(2);
        optionals[1].HasValue.Should().BeFalse();
    }

    [Fact]
    public void UpdateCacheEmitsOptionalSome()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.AddOrUpdate(Create(Key1, Value2));

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(2);
        optionals[1].HasValue.Should().BeTrue();
        optionals[1].Value.Value.Should().Be(Value2);
    }

    [Fact]
    public void UpdateUsesEqualityComparer()
    {
        // having
        var optionalsCS = new List<Optional<KeyValuePair>>();
        var optionalsNonCS = new List<Optional<KeyValuePair>>();
        using var optionalCSObservable = _source.Connect().ToObservableOptional(Key1, CaseSensitiveComparer).Do(optionalsCS.Add).Subscribe();
        using var optionalNonCSObservable = _source.Connect().ToObservableOptional(Key1, CaseInsensitiveComparer).Do(optionalsNonCS.Add).Subscribe();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        _source.AddOrUpdate(Create(Key1, Value1AllCaps));

        // then
        _results.Data.Count.Should().Be(1);
        optionalsNonCS.Count.Should().Be(1);
        optionalsNonCS[0].HasValue.Should().BeTrue();
        optionalsNonCS[0].Value.Value.Should().Be(Value1);
        optionalsCS.Count.Should().Be(2);
        optionalsCS[0].HasValue.Should().BeTrue();
        optionalsCS[0].Value.Value.Should().Be(Value1);
        optionalsCS[1].HasValue.Should().BeTrue();
        optionalsCS[1].Value.Value.Should().Be(Value1AllCaps);
    }

    [Fact]
    public void UpdateWhenReferenceEqualDoesNotEmit()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1).Do(optionals.Add).Subscribe();
        var kvp = Create(Key1, Value1);
        _source.AddOrUpdate(kvp);

        // when
        _source.AddOrUpdate(kvp);
        _source.AddOrUpdate(kvp);
        _source.AddOrUpdate(kvp);

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(1);
        optionals[0].HasValue.Should().BeTrue();
        optionals[0].Value.Value.Should().Be(Value1);
    }

    [Fact]
    public async Task InitialOptionalAvoidsNoneAfterSomeRaceConditions()
    {
        await Task.WhenAll(Enumerable.Range(0, 10000).Select(_ => RunTest()));

        async Task RunTest()
        {
            // having
            using ISourceCache<KeyValuePair, string> source = new SourceCache<KeyValuePair, string>(kvp => kvp.Key);
            var optionals = new List<Optional<KeyValuePair>>();

            // when
            var addTask = Task.Run(() => source.AddOrUpdate(Create(Key1, Value1)));
            using var optionalObservable = source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();
            await addTask;

            // then
            source.Count.Should().Be(1);
            optionals.Count.Should().BeInRange(1, 2);
            optionals.Last().HasValue.Should().BeTrue();
            optionals.Last().Value.Value.Should().Be(Value1);
            if (optionals.Count > 1)
            {
                optionals.First().HasValue.Should().BeFalse();
            }
        }
    }

    [Fact]
    public void InitialOptionalWhenMissingEmitsNone()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // then
        _results.Data.Count.Should().Be(0);
        optionals.Count.Should().Be(1);
        optionals[0].HasValue.Should().BeFalse();
    }

    [Fact]
    public void InitialOptionalWhenPresentEmitsSome()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        _source.AddOrUpdate(Create(Key1, Value1));

        // when
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(1);
        optionals[0].HasValue.Should().BeTrue();
        optionals[0].Value.Value.Should().Be(Value1);
    }

    [Fact]
    public void InitialOptionalWhenAddedEmitsNoneThenSome()
    {
        // having
        var optionals = new List<Optional<KeyValuePair>>();
        using var optionalObservable = _source.Connect().ToObservableOptional(Key1, initialOptionalWhenMissing: true).Do(optionals.Add).Subscribe();

        // when
        _source.AddOrUpdate(Create(Key1, Value1));

        // then
        _results.Data.Count.Should().Be(1);
        optionals.Count.Should().Be(2);
        optionals[0].HasValue.Should().BeFalse();
        optionals[1].HasValue.Should().BeTrue();
        optionals[1].Value.Value.Should().Be(Value1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
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
        completed.Should().Be(completeSource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableFailsIfAndOnlyIfSourceFails(bool failSource)
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
        receivedError.Should().Be(failSource ? testException : default);
    }

    private static KeyValuePair Create(string key, string value) => new(key, value);

    private class KeyValueCompare(IEqualityComparer<string> stringComparer) : IEqualityComparer<KeyValuePair>
    {
        private IEqualityComparer<string> _stringComparer = stringComparer;

        public bool Equals([DisallowNull] KeyValuePair x, [DisallowNull] KeyValuePair y) => _stringComparer.Equals(x.Value, y.Value);
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

