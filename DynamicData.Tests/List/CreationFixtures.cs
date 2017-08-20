using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{

    public class ListCreationFixtures
    {

        [Fact]
        public void Create()
        {
            Task<T> CreateTask<T>(T value) => Task.FromResult(value);

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

            using (var myList = observableChangeset
                .Finally(()=> complete = true)
                .AsObservableList())
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
}
