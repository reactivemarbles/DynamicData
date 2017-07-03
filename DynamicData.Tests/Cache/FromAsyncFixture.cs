using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class FromAsyncFixture
    {
        public TestScheduler Scheduler { get; }

        public  FromAsyncFixture()
        {
            Scheduler = new TestScheduler();
        }

        [Fact]
        public void CanLoadFromTask()
        {
            Task<IEnumerable<Person>> Loader()
            {
                var items = Enumerable.Range(1, 100)
                    .Select(i => new Person("Person" + i, 1))
                    .ToArray()
                    .AsEnumerable();

                return Task.FromResult(items);
            }

            var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>) Loader)
                .ToObservableChangeSet(p=>p.Key)
                .AsObservableCache();

            data.Count.Should().Be(100);
        }

        [Fact]
        public void HandlesErrorsInObservable()
        {
            Task<IEnumerable<Person>> Loader()
            {
                Task.Delay(100);
                throw new Exception("Broken");
            }

            Exception error = null;

            var data = Observable.FromAsync((Func<Task<IEnumerable<Person>>>) Loader)
               .ToObservableChangeSet(p => p.Key)
                .Subscribe((changes) => { }, ex => error = ex);;

            error.Should().NotBeNull();
        }

        [Fact]
        public void HandlesErrorsObservableList()
        {
            Func<Task<IEnumerable<Person>>> loader = () => { throw new Exception("Broken"); };

            Exception error = null;

            var data = Observable.FromAsync(loader)
                .ToObservableChangeSet(p => p.Key)
                .AsObservableCache();

            var subscribed = data.Connect()
                .Subscribe(changes => { }, ex => error = ex);


            error.Should().NotBeNull();
        }

    }
}
