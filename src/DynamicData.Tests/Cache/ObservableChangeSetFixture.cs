using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class ObservableChangeSetFixture
{

    // [Test] //Disabled due to test failing when run with a test runner. Run locally in isolation and it works
    [Description("See https://github.com/reactivemarbles/DynamicData/issues/383")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Acceptable for test.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Disabled due to test failing when run with a test runner")]
    private async Task AsyncSubscriptionCanReceiveMultipleResults()
    {

        //the aim of this test is to ensure we can continuously receive subscriptions when we use the async subscribe overloads
        var result = new List<int>();

        var observable = ObservableChangeSet.Create<int, int>(
                async (changeSet, token) =>
                {
                    var i = 0;

                    while (!token.IsCancellationRequested)
                    {
                        changeSet.AddOrUpdate(i++);

                        /*
                         *  Without ConfigureAwait(false) we get a flakey test which always work when run in isolation
                         *  but periodically fails when all tests are run. WTAF - I have no idea why but can only speculate
                         *  that without it the context is returning to the context of the test runner and it doesn't get back to it
                         *  until after the test session ends
                         */
                        await Task.Delay(5, token).ConfigureAwait(false);
                    }
                },
                i => i)
            .Select(cs => cs.Select(c => c.Current).ToList());

        var isComplete = false;
        Exception? error = null;

        //load list of results
        var subscriber = observable
            .Subscribe(item => result.AddRange(item), ex => error = ex, () => isComplete = true);

        //allow some results through
        await Task.Delay(100);

        await Assert.That(isComplete).IsFalse();
        await Assert.That(error).IsNull();

        //do not try to be clever with timings because wierd stuff happens in time
        await Assert.That(result.Take(5)).IsEquivalentTo(new List<int>
        {
            0,
            1,
            2,
            3,
            4
        });

        subscriber.Dispose();
    }

    [Test]
    public async Task HandlesAsyncError()
    {
        Exception? error = null;

        static Task<IEnumerable<Person>> Loader() => throw new Exception("Broken");

        var observable = ObservableChangeSet.Create<Person, string>(
            async cache =>
            {
                var people = await Loader();
                cache.AddOrUpdate(people);
                return () => { };
            },
            p => p.Name);

        using var dervived = observable.AsObservableCache();
        using (dervived.Connect().Subscribe(_ => { }, ex => error = ex))
        {
            await Assert.That(error).IsNotNull();
        }
    }

    [Test]
    public async Task HandlesError()
    {
        Exception? error = null;

        static IEnumerable<Person> Loader() => throw new Exception("Broken");

        var observable = ObservableChangeSet.Create<Person, string>(
            cache =>
            {
                var people = Loader();
                cache.AddOrUpdate(people);
                return () => { };
            },
            p => p.Name);

        using var derived = observable.AsObservableCache();
        using (derived.Connect().Subscribe(_ => { }, ex => error = ex))
        {
            await Assert.That(error).IsNotNull();
        }
    }

    [Test]
    public async Task LoadsAndDisposeFromObservableCache()
    {
        var isDisposed = false;

        var observable = ObservableChangeSet.Create<Person, string>(cache => () => isDisposed = true, p => p.Name);

        observable.AsObservableCache().Dispose();
        await Assert.That(isDisposed).IsTrue();
    }

    [Test]
    public async Task LoadsAndDisposeUsingAction()
    {
        var isDisposed = false;
        await SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                cache =>
                {
                    var people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                    cache.AddOrUpdate(people);
                    return () => isDisposed = true;
                },
                p => p.Name),
checkContentAction: async result => { await Assert.That(result.Count).IsEqualTo(100); });

        await Assert.That(isDisposed).IsTrue();
    }

    [Test]
    public async Task LoadsAndDisposeUsingActionAsync()
    {
        static Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

        var isDisposed = false;
        await SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                async cache =>
                {
                    var people = await CreateTask();
                    cache.AddOrUpdate(people);
                    return () => isDisposed = true;
                },
                p => p.Name),
checkContentAction: async result => { await Assert.That(result.Count).IsEqualTo(100); });

        await Assert.That(isDisposed).IsTrue();
    }

    [Test]
    public async Task LoadsAndDisposeUsingDisposable()
    {
        var isDisposed = false;
        await SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                cache =>
                {
                    var people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                    cache.AddOrUpdate(people);
                    return Disposable.Create(() => isDisposed = true);
                },
                p => p.Name),
checkContentAction: async result => { await Assert.That(result.Count).IsEqualTo(100); });

        await Assert.That(isDisposed).IsTrue();
    }

    [Test]
    public async Task LoadsAndDisposeUsingDisposableAsync()
    {
        static Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

        var isDisposed = false;
        await SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                async cache =>
                {
                    var people = await CreateTask();
                    cache.AddOrUpdate(people);
                    return Disposable.Create(() => isDisposed = true);
                },
                p => p.Name),
checkContentAction: async result => { await Assert.That(result.Count).IsEqualTo(100); });

        await Assert.That(isDisposed).IsTrue();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private async Task SubscribeAndAssert<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> observableChangeset, bool expectsError = false, Func<IObservableCache<TObject, TKey>, Task>? checkContentAction = null)
        where TKey : notnull
        where TObject : notnull
    {
        Exception? error = null;
        var complete = false;
        IChangeSet<TObject, TKey>? changes = null;

        using (var cache = observableChangeset.Finally(() => complete = true).AsObservableCache())
        using (cache.Connect().Subscribe(result => changes = result, ex => error = ex))
        {
            if (!expectsError)
            {
                await Assert.That(error).IsNull();
            }
            else
            {
                await Assert.That(error).IsNotNull();
            }

            if (checkContentAction is not null) { await checkContentAction(cache); }
        }

        await Assert.That(complete).IsTrue();
    }
}
