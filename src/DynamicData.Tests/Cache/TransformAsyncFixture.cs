#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformAsyncFixture
{
    [Test]
    public async Task Add()
    {
        using var stub = new TransformStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");

        var firstPerson = await stub.TransformFactory(person);

        await Assert.That(stub.Results.Data.Items[0]).IsEqualTo(firstPerson).Because("Should be same person");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        //     Thread.Sleep(10000);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var result = await Task.WhenAll(people.Select(stub.TransformFactory));
        var transformed = result.OrderBy(p => p.Age).ToArray();
        await Assert.That(stub.Results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(transformed, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("each input should be transformed exactly once");
    }

    [Test]
    public async Task Clear()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should be 80 adds");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(100).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task HandleError()
    {
        using var stub = new TransformStub(p => throw new Exception("Broken"));
        stub.Source.AddOrUpdate(new Person("Name1", 1));

        await Assert.That(stub.Results.Error).IsNotNull();
    }

    [Test]
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task RemoveFlowsToTheEnd()
    {
        const int count = 100;
        using var cache = new SourceCache<Person, string>(person => person.Name);
        var allowTransforms = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var adds = 0;
        var removes = 0;
        using var subscription = cache.Connect()
            .TransformAsync(async (Person person, ReactiveUI.Primitives.Optional<Person> previous, string key, CancellationToken token) =>
            {
                await allowTransforms.Task.WaitAsync(token);
                return person;
            })
            .Bind(out var collection)
            .Subscribe(changes =>
            {
                adds += changes.Adds;
                removes += changes.Removes;
                if (removes == count)
                    finished.TrySetResult();
            }, error => finished.TrySetException(error));

        for (var index = 0; index < count; index++)
        {
            var person = new Person("Name" + index, index);
            cache.AddOrUpdate(person);
            cache.RemoveKey(person.Name);
        }

        allowTransforms.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Assert.That(adds).IsEqualTo(count);
        await Assert.That(removes).IsEqualTo(count);
        await Assert.That(collection).IsEmpty();
    }

    [Test]
    public async Task ReTransformAll()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new ReactiveUI.Primitives.Signals.Signal<Unit>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(Unit.Default);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(10);

        for (var i = 1; i <= 10; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;

            await Assert.That(updated).IsEqualTo(original);
            await Assert.That(ReferenceEquals(original, updated)).IsFalse();
        }
    }

    [Test]
    public async Task ReTransformSelected()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new ReactiveUI.Primitives.Signals.Signal<Func<Person, bool>>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(person => person.Age <= 5);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(5);

        for (var i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            await Assert.That(updated).IsEqualTo(original);
            await Assert.That(ReferenceEquals(original, updated)).IsFalse();
        }
    }

    [Test]
    public async Task SameKeyChanges()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should return 1 adds");
        await Assert.That(stub.Results.Messages[0].Updates).IsEqualTo(9).Because("Should return 9 adds");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should result in 1 record");

        var lastTransformed = await stub.TransformFactory(people.Last());
        var onlyItemInCache = stub.Results.Data.Items[0];

        await Assert.That(onlyItemInCache).IsEqualTo(lastTransformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(newperson);
        stub.Source.AddOrUpdate(updated);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(1).Because("Should be 1 update");
    }

    [Test, Arguments(true), Arguments(false)]
    public async Task TransformOnRefresh(bool transformOnRefresh)
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .AutoRefresh()
            .TransformAsync((p, key) => Task.FromResult(new PersonWithAgeGroup(p, p.Age < 18 ? "Child" : "Adult")), TransformAsyncOptions.Default with { TransformOnRefresh = transformOnRefresh }).AsAggregator();

        var person = new Person("SomeOne", 16);
        source.AddOrUpdate(person);

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Lookup("SomeOne").Value.AgeGroup).IsEqualTo("Child");

        person.Age = 21;

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Lookup("SomeOne").Value.AgeGroup).IsEqualTo(transformOnRefresh ? "Adult" : "Child");

    }

    [Test]
    public async Task TransformAsyncCancelsTokenOnUnSubscribe()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        var tcs = new TaskCompletionSource<Person>();
        using var sub = source.Connect()
            .TransformAsync(async (c, p, key, cancel) =>
            {
                using (cancel.Register(() => tcs.SetCanceled(), useSynchronizationContext: false))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            })
            .Subscribe();

        source.AddOrUpdate(new Person());

        sub.Dispose();
        await Assert.That(tcs.Task.IsCanceled).IsTrue();
    }


    [Test, Arguments(10), Arguments(100)]

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
            }, TransformAsyncOptions.Default with { MaximumConcurrency = maxConcurrency }).AsAggregator();

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
