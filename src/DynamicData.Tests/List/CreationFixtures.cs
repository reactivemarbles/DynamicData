namespace DynamicData.Tests.List;

public class ListCreationFixtures
{
    [Fact]
    public void Create()
    {
        static Task<T> CreateTask<T>(T value) => Task.FromResult(value);

        SubscribeAndAssert(
            ObservableChangeSet.Create<int>(
                async list =>
                {
                    var value = await CreateTask<int>(10);
                    list.Add(value);
                    return () => { };
                }));
    }

    [Fact]
    public void TaskCompletesTheStreamAfterItsChanges()
    {
        var task = new TaskCompletionSource();
        ISourceList<int>? captured = null;

        using var subscription = ObservableChangeSet.Create<int>((list, _) => { captured = list; return task.Task; }).RecordListItems(out var results);

        captured!.Add(10);
        results.HasCompleted.Should().BeFalse("the task has not completed yet");

        task.SetResult();

        results.RecordedItems.Should().Equal([10], "changes made by the task arrive ahead of completion");
        results.HasCompleted.Should().BeTrue("nothing can follow the end of the task");
        results.Error.Should().BeNull();
    }

    [Fact]
    public void TaskFailureIsDeliveredOnTheThreadThatFailsIt()
    {
        var task = new TaskCompletionSource();

        using var subscription = ObservableChangeSet.Create<int>((_, _) => task.Task).RecordListItems(out var results);

        var expectedError = new Exception("Test Exception");
        task.SetException(expectedError);

        results.Error.Should().BeSameAs(expectedError, "the failure should arrive before SetException returns, not later on another thread");
    }

    [Fact]
    public void TaskThatFinishesDuringSubscriptionStillDeliversItsChanges()
    {
        using var subscription = ObservableChangeSet.Create<int>((list, _) => { list.Add(10); return Task.CompletedTask; }).RecordListItems(out var results);

        results.RecordedItems.Should().Equal([10], "the change was made before the task finished");
        results.HasCompleted.Should().BeTrue();
        results.Error.Should().BeNull();
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private void SubscribeAndAssert<T>(IObservable<IChangeSet<T>> observableChangeset, bool expectsError = false)
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
                error.Should().BeNull();
            }
            else
            {
                error.Should().NotBeNull();
            }
        }

        complete.Should().BeTrue();
    }
}
