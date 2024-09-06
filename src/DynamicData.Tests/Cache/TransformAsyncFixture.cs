using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformAsyncFixture
{
    [Fact]
    public async Task Add()
    {
        using var stub = new TransformStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var firstPerson = await stub.TransformFactory(person);

        stub.Results.Data.Items[0].Should().Be(firstPerson, "Should be same person");
    }

    [Fact]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        //     Thread.Sleep(10000);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var result = await Task.WhenAll(people.Select(stub.TransformFactory));
        var transformed = result.OrderBy(p => p.Age).ToArray();
        stub.Results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(stub.Results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
    }

    [Fact]
    public void Clear()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should be 80 adds");
        stub.Results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void HandleError()
    {
        using var stub = new TransformStub(p => throw new Exception("Broken"));
        stub.Source.AddOrUpdate(new Person("Name1", 1));

        stub.Results.Error.Should().NotBeNull();
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        stub.Results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public async Task RemoveFlowsToTheEnd()
    {
        var transform = 0;
        var count = 100;
        ReadOnlyObservableCollection<Person> collection;

        var cache = new SourceCache<Person, string>(p => p.Name);
        var people = Enumerable.Range(1, count).Select(l => new Person("Name" + l, l)).ToArray();

        cache.Connect()
            .TransformAsync(async person =>
            {
                try
                {
                    await Task.Delay(Random.Shared.Next(1, 12));
                    return person;
                }
                finally
                {
                    transform++;
                }
            })
            .Bind(out collection)
            .Subscribe();

        foreach (var p in people)
        {
            cache.AddOrUpdate(p);
            cache.RemoveKey(p.Name);
        }

        await collection.ToObservableChangeSet().Take(count * 2);

        collection.Count.Should().Be(0);
    }

    [Fact]
    public void ReTransformAll()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new Subject<Unit>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(Unit.Default);

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Messages[1].Updates.Should().Be(10);

        for (var i = 1; i <= 10; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;

            updated.Should().Be(original);
            ReferenceEquals(original, updated).Should().BeFalse();
        }
    }

    [Fact]
    public void ReTransformSelected()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new Subject<Func<Person, bool>>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(person => person.Age <= 5);

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Messages[1].Updates.Should().Be(5);

        for (var i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            updated.Should().Be(original);
            ReferenceEquals(original, updated).Should().BeFalse();
        }
    }

    [Fact]
    public async Task SameKeyChanges()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        stub.Source.AddOrUpdate(people);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should return 1 adds");
        stub.Results.Messages[0].Updates.Should().Be(9, "Should return 9 adds");
        stub.Results.Data.Count.Should().Be(1, "Should result in 1 record");

        var lastTransformed = await stub.TransformFactory(people.Last());
        var onlyItemInCache = stub.Results.Data.Items[0];

        onlyItemInCache.Should().Be(lastTransformed, "Incorrect transform result");
    }

    [Fact]
    public void Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(newperson);
        stub.Source.AddOrUpdate(updated);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        stub.Results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
    }


   

    [Theory, InlineData(true), InlineData(false)]
    public void TransformOnRefresh(bool transformOnRefresh)
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .AutoRefresh()
            .TransformAsync((p, key) => Task.FromResult(new PersonWithAgeGroup(p, p.Age < 18  ? "Child" : "Adult")), TransformAsyncOptions.Default with { TransformOnRefresh = transformOnRefresh }).AsAggregator();

        var person = new Person("SomeOne", 16);
        source.AddOrUpdate(person);

        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be("Child");
        
        person.Age = 21;


        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be(transformOnRefresh ? "Adult": "Child");

    }

    
    [Theory, InlineData(10), InlineData(100)]

    public async Task WithMaxConcurrency(int maxConcurrency)
    {
        /* We need to test whether the max concurrency has any effect.

             If  maxConcurrency == 100, this test takes a little more than 100 ms
             If maxConcurrency = 10, this test takes a little more than 1s 

            So it works, but how can it be tested in a scientific way ??
        */


        const int transformCount = 100;

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .TransformAsync(async (p, key) =>
            {
                await Task.Delay(100);

                return new PersonWithAgeGroup(p, p.Age < 18 ? "Child" : "Adult");
            },  TransformAsyncOptions.Default with { MaximumConcurrency = maxConcurrency }).AsAggregator();

        source.AddOrUpdate(Enumerable.Range(1, transformCount).Select(l => new Person("Person" + l, l)));

        await results.Data.CountChanged.Where(c => c == transformCount).Take(1);
    }

    private class TransformStub : IDisposable
    {
        public TransformStub()
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformAsync(TransformFactory));
        }

        public TransformStub(Func<Person, PersonWithGender> factory)
        {
            TransformFactory = (p) =>
            {
                var result = factory(p);
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformAsync(TransformFactory));
        }

        public TransformStub(IObservable<Unit> retransformer)
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(
                Source.Connect().TransformAsync(
                    TransformFactory,
                    retransformer.Select(
                        x =>
                        {
                            Func<Person, string, bool> transformer = (p, key) => true;
                            return transformer;
                        })));
        }

        public TransformStub(IObservable<Func<Person, bool>> retransformer)
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(
                Source.Connect().TransformAsync(
                    TransformFactory,
                    retransformer.Select(
                        selector =>
                        {
                            Func<Person, string, bool> transformed = (p, key) => selector(p);
                            return transformed;
                        })));
        }

        public ChangeSetAggregator<PersonWithGender, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Func<Person, Task<PersonWithGender>> TransformFactory { get; }

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
