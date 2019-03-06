using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{

    public class ObservableChangeSetFixture
    {

        [Fact]
        public void LoadsAndDisposeUsingAction()
        {
            bool isDisposed = false;
            SubscribeAndAssert(ObservableChangeSet.Create<Person, string>(cache =>
            {
                Person[] people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                cache.AddOrUpdate(people);
                return () => { isDisposed = true; };
            }, p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

            isDisposed.Should().BeTrue();
        }

        [Fact]
        public void LoadsAndDisposeFromObservableCache()
        {
            bool isDisposed = false;

           var observable = ObservableChangeSet.Create<Person, string>(cache =>
                {
                    return () => { isDisposed = true; };
                }, p => p.Name);

            observable.AsObservableCache().Dispose();
            isDisposed.Should().BeTrue();
        }

        [Fact]
        public void LoadsAndDisposeUsingDisposable()
        {
            bool isDisposed = false;
            SubscribeAndAssert(ObservableChangeSet.Create<Person, string>(cache =>
            {
                Person[] people = Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray();
                cache.AddOrUpdate(people);
                return Disposable.Create(()=> { isDisposed = true; });
            }, p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

            isDisposed.Should().BeTrue();
        }

        [Fact]
        public void LoadsAndDisposeUsingActionAsync()
        {
            Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

            bool isDisposed = false;
            SubscribeAndAssert(ObservableChangeSet.Create<Person, string>(async cache =>
            {
                var people = await CreateTask();
                cache.AddOrUpdate(people);
                return () => { isDisposed = true; };
            }, p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

            isDisposed.Should().BeTrue();
        }


        [Fact]
        public void LoadsAndDisposeUsingDisposableAsync()
        {
            Task<Person[]> CreateTask() => Task.FromResult(Enumerable.Range(1, 100).Select(i => new Person($"Name.{i}", i)).ToArray());

            bool isDisposed = false;
            SubscribeAndAssert(ObservableChangeSet.Create<Person, string>(async cache =>
            {
                var people = await CreateTask();
                cache.AddOrUpdate(people);
                return Disposable.Create(() => { isDisposed = true; });
            }, p => p.Name),
            checkContentAction: result => result.Count.Should().Be(100));

            isDisposed.Should().BeTrue();
        }

        [Fact]
        public void HandlesAsyncError()
        {
            Exception error = null;
            Task<IEnumerable<Person>> Loader()
            {
                throw new Exception("Broken");
            }
            
          var observable =   ObservableChangeSet.Create<Person, string>(async cache =>
            {
                var people = await Loader();
                cache.AddOrUpdate(people);
                return () => { };
            }, p => p.Name);


            using (var dervived = observable.AsObservableCache())
            using (dervived.Connect().Subscribe(_ => { }, ex => error = ex ))
            {
                error.Should().NotBeNull();
            }
        }

        [Fact]
        public void HandlesError()
        {
            Exception error = null;
            IEnumerable<Person> Loader()
            {
                throw new Exception("Broken");
            }

            var observable = ObservableChangeSet.Create<Person, string>(cache =>
            {
                var people =  Loader();
                cache.AddOrUpdate(people);
                return () => { };
            }, p => p.Name);


            using (var dervived = observable.AsObservableCache())
            using (dervived.Connect().Subscribe(_ => { }, ex => error = ex))
            {
                error.Should().NotBeNull();
            }
        }


        private void SubscribeAndAssert<TObject,TKey>(IObservable<IChangeSet<TObject, TKey>> observableChangeset,  
            bool expectsError = false,
            Action<IObservableCache<TObject, TKey>> checkContentAction = null)
        {
            Exception error = null;
            bool complete = false;
            IChangeSet<TObject,TKey> changes = null;

            using (var cache = observableChangeset.Finally(()=> complete = true).AsObservableCache())
            using (cache.Connect().Subscribe(result => changes = result, ex => error = ex))
            {         
                if (!expectsError)
                {
                    error.Should().BeNull();
                }
                else
                {
                    error.Should().NotBeNull();
                }

                checkContentAction?.Invoke(cache);
            }
            complete.Should().BeTrue();
        }
    }
}
