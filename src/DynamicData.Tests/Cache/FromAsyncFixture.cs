using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class FromAsyncFixture
{
    public FromAsyncFixture() => Scheduler = new TestScheduler();

    public TestScheduler Scheduler { get; }

    [Test]
    public async Task CanLoadFromTask()
    {
        Task<IEnumerable<Person>> Loader()
        {
            var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray().AsEnumerable();

            return Task.FromResult(items);
        }

        var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>)Loader).ToObservableChangeSet(p => p.Key).AsObservableCache();

        await Assert.That(data.Count).IsEqualTo(100);
    }

    [Test]
    public async Task HandlesErrorsInObservable()
    {
        Task<IEnumerable<Person>> Loader()
        {
            Task.Delay(100);
            throw new Exception("Broken");
        }

        Exception? error = null;

        var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>)Loader).ToObservableChangeSet(p => p.Key).Subscribe((changes) => { }, ex => error = ex);

        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task HandlesErrorsObservableList()
    {
        Task<IEnumerable<Person>> Loader()
        {
            throw new Exception("Broken");
        }

        Exception? error = null;

        var data = Observable.FromAsync(Loader).ToObservableChangeSet(p => p.Key).Subscribe(changes => { }, ex => error = ex);

        var data2 = Observable.FromAsync(Loader).ToObservableChangeSet(p => p.Key).AsObservableCache().Connect().Subscribe(changes => { }, ex => error = ex);

        //var subscribed = data.Connect()
        //

        await Assert.That(error).IsNotNull();
    }
}
