using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ObservableChangeSetFixture
{

   // [Fact] //Disabled due to test failing when run with a test runner. Run locally in isolation and it works
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

        isComplete.Should().Be(false);
        error.Should().BeNull();

        //do not try to be clever with timings because wierd stuff happens in time
        result.Take(5).Should().BeEquivalentTo(new List<int>
        {
            0,
            1,
            2,
            3,
            4
        });

        subscriber.Dispose();
    }


    [Fact]
    public void HandlesAsyncError()
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
            error.Should().NotBeNull();
        }
    }

    [Fact]
    public void HandlesError()
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
            error.Should().NotBeNull();
        }
    }

    [Fact]
    public void LoadsAndDisposeFromObservableCache()
    {
        var isDisposed = false;

        var observable = ObservableChangeSet.Create<Person, string>(cache => () => isDisposed = true, p => p.Name);

        observable.AsObservableCache().Dispose();
        isDisposed.Should().BeTrue();
    }

    [Fact]
    public void LoadsAndDisposeUsingAction()
    {
        var isDisposed = false;
        SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                cache =>
                {
                    var people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                    cache.AddOrUpdate(people);
                    return () => isDisposed = true;
                },
                p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

        isDisposed.Should().BeTrue();
    }

    [Fact]
    public void LoadsAndDisposeUsingActionAsync()
    {
        static Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

        var isDisposed = false;
        SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                async cache =>
                {
                    var people = await CreateTask();
                    cache.AddOrUpdate(people);
                    return () => isDisposed = true;
                },
                p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

        isDisposed.Should().BeTrue();
    }

    [Fact]
    public void LoadsAndDisposeUsingDisposable()
    {
        var isDisposed = false;
        SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                cache =>
                {
                    var people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                    cache.AddOrUpdate(people);
                    return Disposable.Create(() => isDisposed = true);
                },
                p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

        isDisposed.Should().BeTrue();
    }

    [Fact]
    public void LoadsAndDisposeUsingDisposableAsync()
    {
        static Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

        var isDisposed = false;
        SubscribeAndAssert(
            ObservableChangeSet.Create<Person, string>(
                async cache =>
                {
                    var people = await CreateTask();
                    cache.AddOrUpdate(people);
                    return Disposable.Create(() => isDisposed = true);
                },
                p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

        isDisposed.Should().BeTrue();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private void SubscribeAndAssert<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> observableChangeset, bool expectsError = false, Action<IObservableCache<TObject, TKey>>? checkContentAction = null)
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
                error.Should().BeNull();
            }
            else
            {
                error.Should().NotBeNull();
            }

            checkContentAction?.Invoke(cache);
        }

        complete.Should().BeTrue();
    }
}
