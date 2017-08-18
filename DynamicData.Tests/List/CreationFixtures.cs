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

         

            //Load list or cache from observables
            IObservable<IEnumerable<Person>> LoadPeople() =>Observable.Return(Enumerable.Empty<Person>());
            IObservable<IEnumerable<Person>> SubscribePeople() => Observable.Return(Enumerable.Empty<Person>());

            IObservable<IChangeSet<Person>> listFromObservable = ObservableChangeSet.Create<Person>(list =>
            {
                var peopleLoader = LoadPeople().Subscribe(list.AddRange, list.OnError);
                var peopleSubscriber = SubscribePeople().Subscribe(list.AddRange, list.OnError);
                return new CompositeDisposable(peopleLoader, peopleSubscriber);
            });

            IObservable<IChangeSet<Person, string>> cacheFromObservable = ObservableChangeSet.Create<Person, string>(cache =>
            {
                var peopleLoader = LoadPeople().Subscribe(cache.AddOrUpdate, cache.OnError);
                var peopleSubscriber = SubscribePeople().Subscribe(cache.AddOrUpdate, cache.OnError);

                return new CompositeDisposable(peopleLoader, peopleSubscriber);
            }, p=>p.Name);


            //Load list or cache from tasks
            Task<IEnumerable<Person>> LoadPeopleAsync() => Task.FromResult(Enumerable.Empty<Person>());

            IObservable<IChangeSet<Person>> listFromTask = ObservableChangeSet.Create<Person>(async list =>
            {
                var people =await LoadPeopleAsync();
                list.AddRange(people);
            });

            IObservable<IChangeSet<Person, string>> cacheFromTask = ObservableChangeSet.Create<Person, string>(async cache =>
            {
                var people = await LoadPeopleAsync();
                cache.AddOrUpdate(people);
            }, p => p.Name);




            var myObservableCache = ObservableChangeSet.Create<Person, string>(cache =>
            {



                return new CompositeDisposable();
            }, p => p.Name);
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
