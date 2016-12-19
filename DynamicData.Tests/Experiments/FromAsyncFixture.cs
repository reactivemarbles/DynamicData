using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using NUnit.Framework;


namespace DynamicData.Tests.Experiments
{
    [TestFixture]
    public class FromAsyncFixture
    {

        [Test]
        public void CanLoadFromTask()
        {
            Func<Task<IEnumerable<Person>>> loader = () =>
            {
                Task.Delay(100);

                var items = Enumerable.Range(1, 100)
                    .Select(i => new Person("Person" + i, 1))
                    .ToArray()
                    .AsEnumerable();

                return Task.FromResult(items);
            };

            var data = Observable.FromAsync(loader)
                .ToObservableChangeSet()
                .AsObservableList();

            Assert.AreEqual(100, data.Count);
        }

        [Test]
        public void HandlesErrorsInObservable()
        {

            Func<Task<IEnumerable<Person>>> loader = () =>
            {
                Task.Delay(100);
                throw new Exception("Broken");
            };

            Exception error = null;

            var data = Observable.FromAsync(loader)
                .ToObservableChangeSet()
                .Subscribe((changes) => { }, ex => error = ex);;

            Assert.IsNotNull(error);
        }

        [Test]
        public void HandlesErrorsObservableList()
        {

            Func<Task<IEnumerable<Person>>> loader = () =>
            {
                Task.Delay(100);
                throw new Exception("Broken");
            };

            Exception error = null;

            var data = Observable.FromAsync(loader)
                .ToObservableChangeSet()
                .AsObservableList();

            var xxx = data.Connect()
                        .Subscribe(changes=> {}, ex =>  error = ex);


            Assert.IsNotNull(error);
        }

    }
}
