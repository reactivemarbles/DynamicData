using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class FromAsyncFixture
{
    private readonly TestScheduler _scheduler;

    public FromAsyncFixture() => _scheduler = new TestScheduler();

    [Test]
    public async Task CanLoadFromTask()
    {
        Task<IEnumerable<Person>> Loader()
        {
            var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray().AsEnumerable();

            return Task.FromResult(items);
        }

        var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>)Loader).ToObservableChangeSet().AsObservableList();

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

        var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>)Loader).ToObservableChangeSet().Subscribe((changes) => { }, ex => error = ex);

        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task HandlesErrorsObservableList()
    {
        Task<IEnumerable<Person>> Loader()
        {
            Task.Delay(100);
            throw new Exception("Broken");
        }

        Exception? error = null;

        var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>)Loader).ToObservableChangeSet().AsObservableList();

        var subscribed = data.Connect().Subscribe(changes => { }, ex => error = ex);

        await Assert.That(error).IsNotNull();
    }
}
