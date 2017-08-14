using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{

    public class ListCreationFixtures
    {

        [Fact]
        public void Create()
        {
            SubscribeAndAssert(ObservableChangeSet.Create<int>(async list =>
            {
                var value = await CreateTask<int>(10);
                list.Add(value);
                return () => { };
            }));
        }

        private void SubscribeAndAssert<T>(IObservable<IChangeSet<T>> observableChangeset, bool expectsError = false)
        {
            Exception error = null;
            bool complete = false;
            IChangeSet<T> changes = null;

            using (var myList = observableChangeset.AsObservableList())
            using (myList.Connect().Subscribe(result => changes = result, ex => error = ex, () => complete = true))
            {         
                if (!expectsError)
                {
                    error.Should().BeNull();
                }
                else
                {
                    error.Should().NotBeNull();
                }

                myList.Dispose();
                complete.Should().BeTrue();
            }
        }


        private Task<T> CreateTask<T>(T value)
        {
            return Task.FromResult(value);
        }
    }
}
