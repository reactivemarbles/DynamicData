namespace DynamicData.Tests.List;

public class ListCreationFixtures
{
    [Test]
    public async Task Create()
    {
        static Task<T> CreateTask<T>(T value) => Task.FromResult(value);

        await SubscribeAndAssert(
            ObservableChangeSet.Create<int>(
                async list =>
                {
                    var value = await CreateTask(10);
                    list.Add(value);
                    return () => { };
                }));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private async Task SubscribeAndAssert<T>(IObservable<IChangeSet<T>> observableChangeset, bool expectsError = false)
        where T : notnull
    {
        Exception? error = null;
        var complete = false;
        IChangeSet<T>? changes = null;

        using (var myList = observableChangeset.Finally(() => complete = true).AsObservableList())
        using (myList.Connect().Subscribe(result => changes = result, ex => error = ex))
        {
            if (!expectsError)
            {
                await Assert.That(error).IsNull();
            }
            else
            {
                await Assert.That(error).IsNotNull();
            }
        }

        await Assert.That(complete).IsTrue();
    }
}
