using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
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
