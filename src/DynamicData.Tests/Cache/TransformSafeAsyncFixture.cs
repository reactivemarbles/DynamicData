using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData.Cache;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformSafeAsyncFixture
{
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

        for (int i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            updated.Should().Be(original);
            ReferenceEquals(original, updated).Should().BeFalse();
        }
    }

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
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 80 adds");
        stub.Results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using (var stub = new TransformStub())
        {
            stub.Source.AddOrUpdate(newperson);
            stub.Source.AddOrUpdate(updated);

            stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
            stub.Results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
            stub.Results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
        }
    }

    [Fact]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var result = await Task.WhenAll(people.Select(stub.TransformFactory));
        var transformed = result.OrderBy(p => p.Age).ToArray();
        stub.Results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(stub.Results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
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
    public void Clear()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should be 80 addes");
        stub.Results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void HandleError()
    {
        using var stub = new TransformStub(p =>
        {
            if (p.Age <= 50)
                return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

            throw new Exception("Broken");
        });
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        stub.Source.AddOrUpdate(people);

        stub.Results.Error.Should().BeNull();

        Exception? error = null;
        stub.Source.Connect()
            .Subscribe(changes => { }, ex => error = ex);

        error.Should().BeNull();

        stub.HandledErrors.Count.Should().Be(50);
        stub.Results.Data.Count.Should().Be(50);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void TransformOnRefresh(bool transformOnRefresh)
    {
        int errorCount = 0;
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .AutoRefresh()
            .TransformAsync((p, key) => Task.FromResult(new PersonWithAgeGroup(p, p.Age < 18 ? "Child" : "Adult")), TransformAsyncOptions.Default with { TransformOnRefresh = transformOnRefresh }).AsAggregator();

        var person = new Person("SomeOne", 16);
        source.AddOrUpdate(person);

        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be("Child");

        person.Age = 21;


        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be(transformOnRefresh ? "Adult" : "Child");
        errorCount.Should().Be(0);
    }


    [Theory, InlineData(10), InlineData(100)]

    public async Task WithMaxConcurrency(int maxConcurrency)
    {
        /* We need to test whether the max concurrency has any effect.

             If  maxConcurrency == 100, this test takes a little more than 100 ms
             If maxConcurrency = 10, this test takes a little more than 1s 

            So it works, but how can it be tested in a scientific way ??
        */

        int errorCount = 0;
        const int transformCount = 100;

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .TransformSafeAsync(async (p, key) =>
                {
                    await Task.Delay(100);

                    return new PersonWithAgeGroup(p, p.Age < 18 ? "Child" : "Adult");
                }
                , error => { errorCount++; }
                , TransformAsyncOptions.Default with { MaximumConcurrency = maxConcurrency })
            .AsAggregator();


        source.AddOrUpdate(Enumerable.Range(1, transformCount).Select(l => new Person("Person" + l, l)));


        await results.Data.CountChanged.Where(c => c == transformCount).Take(1);

        errorCount.Should().Be(0);
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

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler));
        }

        public TransformStub(Func<Person, PersonWithGender> factory)
        {
            TransformFactory = (p) =>
            {
                var result = factory(p);
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler));
        }

        public TransformStub(IObservable<Unit> retransformer)
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(
                Source.Connect().TransformSafeAsync(
                    TransformFactory,
                    ErrorHandler,
                    retransformer.Select(
                        x =>
                        {
                            bool Transformer(Person p, string key) => true;
                            return (Func<Person, string, bool>)Transformer;
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
                Source.Connect().TransformSafeAsync(
                    TransformFactory,
                    ErrorHandler,
                    retransformer.Select(
                        selector =>
                        {
                            bool Transformed(Person p, string key) => selector(p);
                            return (Func<Person, string, bool>)Transformed;
                        })));
        }

        public IList<Error<Person, string>> HandledErrors { get; } = new List<Error<Person, string>>();

        public ChangeSetAggregator<PersonWithGender, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Func<Person, Task<PersonWithGender>> TransformFactory { get; }

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }

        private void ErrorHandler(Error<Person, string> error) => HandledErrors.Add(error);
    }
}
