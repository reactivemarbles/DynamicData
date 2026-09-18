#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformSafeAsyncFixture
{
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

        for (int i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            await Assert.That(updated).IsEqualTo(original);
            await Assert.That(ReferenceEquals(original, updated)).IsFalse();
        }
    }

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
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 adds");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using (var stub = new TransformStub())
        {
            stub.Source.AddOrUpdate(newperson);
            stub.Source.AddOrUpdate(updated);

            await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
            await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
            await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(1).Because("Should be 1 update");
        }
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var result = await Task.WhenAll(people.Select(stub.TransformFactory));
        var transformed = result.OrderBy(p => p.Age).ToArray();
        await Assert.That(stub.Results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(stub.Results.Data.Items.OrderBy(p => p.Age)).Because("Incorrect transform result");
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
    public async Task Clear()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should be 80 addes");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(100).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task HandleError()
    {
        using var stub = new TransformStub(p =>
        {
            if (p.Age <= 50)
                return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

            throw new Exception("Broken");
        });
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Error).IsNull();

        Exception? error = null;
        stub.Source.Connect()
            .Subscribe(changes => { }, ex => error = ex);

        await Assert.That(error).IsNull();

        await Assert.That(stub.HandledErrors.Count).IsEqualTo(50);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(50);
    }

    [Test, Arguments(true), Arguments(false)]
    public async Task TransformOnRefresh(bool transformOnRefresh)
    {
        int errorCount = 0;
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
        await Assert.That(errorCount).IsEqualTo(0);
    }


    [Test]
    public async Task TransformSafeAsyncCancelsTokenOnUnSubscribe()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        var tcs = new TaskCompletionSource<Person>();
        using var sub = source.Connect()
            .TransformSafeAsync(async (c, p, key, cancel) =>
            {
                using (cancel.Register(() => tcs.SetCanceled(), useSynchronizationContext: false))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            },
            error => Assert.Fail($"Unexpected error: {error}")) // NOTE: Cancellation exception should not be called because the handler should be torn down with subscription
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

        await Assert.That(errorCount).IsEqualTo(0);
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
