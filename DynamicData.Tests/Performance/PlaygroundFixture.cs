#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Experimental;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

#endregion

namespace DynamicData.Tests.Performance
{

    [TestFixture(Ignore = true)]
    class PlaygroundFixture
    {


        private ISourceCache<Person, string> _cache;
        private RandomPersonGenerator _generator = new RandomPersonGenerator();
        
        [SetUp]
        public void SetStream()
        {
           _cache = new SourceCache<Person, string>(p=>p.Name);
        }

 
        [TearDown]
        public void CleanUp()
        {
            if (_cache != null)
                _cache.Dispose();
        }
        [Test]
        public void SortingError()
        {
            var scheduler = new EventLoopScheduler();
            var thread2 = new EventLoopScheduler();

            var watcher = _cache.Connect().AsWatcher(new TaskPoolScheduler(Task.Factory));

            scheduler.Schedule(() => _cache.BatchUpdate(updater => 
            {
                Console.WriteLine("updating");
                updater.AddOrUpdate(_generator.Take(1000));
                Console.WriteLine("updated");
            }));

            for (int i = 0; i < 10; i++)
            {
                thread2.Schedule(TimeSpan.FromMilliseconds(10), () => _cache.BatchUpdate(updater =>
                {
                    Console.WriteLine("updating from thread 2");
                    updater.AddOrUpdate(_generator.Take(10));
                    Console.WriteLine("updated from thread 2");
                }));
            }
            var result = new List<IObservableCache<SelfObservingPerson,string>>();

            for (int i = 0; i < 5; i++)
            {

                Console.WriteLine("subscribing {0}", i);
                ////var connection = _cache.Connect().Group(p=>p.Age).AsObservableCache();
                int i2 = i;
                var connection = _cache.Connect()
                    .Sort(new PersonSorter())
                    // .Filter(p => p.Age>25)
                    //.Group(p => p.Age)
                    // .AsObservableCache()

                    //   .Connect()
                    .SubscribeOn(new TaskPoolScheduler(Task.Factory))
                    .Do(updates => Console.WriteLine("Received {0} updates for {1}", updates.Count, i2))
                    .AsObservableCache();

              //  result.Add(connection);
                Console.WriteLine("subscribed {0}",i);
                int i1 = i;

            }
            var connection3 = _cache.Connect()
                .Subscribe(updates => Console.WriteLine("Main Thread Received {0} updates", updates.Count));
            Console.WriteLine("Complete");

            Thread.Sleep(TimeSpan.FromSeconds(1));

            foreach (var cache in result)
            {
               Console.WriteLine("{0} items in the cache",cache.Count);
                Assert.IsTrue(cache.Items.All(p => p.Person != null));

            }

            scheduler.Dispose();
            thread2.Dispose();
            
        }

        [Test]
        public void TryAndMakeDeadlock()
        {
            var scheduler = new EventLoopScheduler();
            var thread2 = new EventLoopScheduler();

            var watcher = _cache.Connect().AsWatcher(new TaskPoolScheduler(Task.Factory));

            _cache.BatchUpdate(updater =>
            {
                Console.WriteLine("updating");
                updater.AddOrUpdate(_generator.Take(11000));
                Console.WriteLine("updated");
            });

            var connections = new List<IObservableCache<SelfObservingPerson, string>> ();
            for (int i = 0; i < 10; i++)
            {
                    _cache.BatchUpdate(updater =>
                    {
                        Console.WriteLine("updating from thread 2");
                        updater.AddOrUpdate(_generator.Take(10));
                        Console.WriteLine("updated from thread 2");
                    });

                    var connection = _cache.Connect()
                    .Transform(u => new SelfObservingPerson(watcher.Watch(u.Key).Select(p => p.Current)))
                    .SubscribeOn(new TaskPoolScheduler(Task.Factory))
                    .AsObservableCache();

                connections.Add(connection);
            }
            Thread.Sleep(2);

            scheduler.Dispose();
            thread2.Dispose();

        }



        [Test]
        public void SubscribedToServeral()
        {
            var watcher = _cache.Connect().AsWatcher(new TaskPoolScheduler(Task.Factory));
            var people = _generator.Take(10).ToList();
  
            var subscriber1 = watcher.Watch(people[0].Key).Subscribe(p=> Console.WriteLine(p.Current));
            var subscriber2 = watcher.Watch(people[0].Key).Subscribe(p => Console.WriteLine(p.Current));
            var subscriber3 = watcher.Watch(people[0].Key).Subscribe(p => Console.WriteLine(p.Current));

            _cache.BatchUpdate(updater =>
            {
                Console.WriteLine("updating");
                updater.AddOrUpdate(people);
                Console.WriteLine("updated");
            });


            Thread.Sleep(TimeSpan.FromSeconds(1));
            subscriber1.Dispose();
            subscriber2.Dispose();
            subscriber3.Dispose();

            var subscriber4 = watcher.Watch(people[0].Key)
                .Take(1)
                .Subscribe(p => Console.WriteLine(p.Current));
        
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public void AsyncSubscriber()
        {
           // var watcher = _cache.Connect().AsAsyncWatch(new TaskPoolScheduler(Task.Factory));
            var scheduler = new EventLoopScheduler();
            var watcher = _cache.Connect().AsWatcher(scheduler);
            const int count = 10000;
            
            scheduler.Schedule(() => _cache.BatchUpdate(updater =>
            {
                Console.WriteLine("updating");
                updater.AddOrUpdate(_generator.Take(count));
                Console.WriteLine("updated");
            }));

            var transformed = _cache.Connect().Transform(p =>
            {
                return new SelfObservingPerson(watcher.Watch(p.Key).Select(w => w.Current));
            })
            .Do(updates => Console.WriteLine("Transformed {0} updates", updates.Count))
            .DisposeMany()
            .AsObservableCache();

            Console.WriteLine("Complete");
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.AreEqual(count,transformed.Count,"Count should be {0} but is {1}",count,transformed.Count);
            foreach (var item in transformed.KeyValues)
            {
                Assert.IsNotNull(item.Value.Person,"Person {0} has not been set",item.Key);
            }

            watcher.Dispose();
            transformed.Dispose();
            scheduler.Dispose();

        }

        [Test]
        public void ScanTest()
        {
            SetStream();
            var abc = _cache.Connect().ScanCache(new HashSet<Person>(), (x) => x.ToHashSet())
                .Subscribe(result => Console.WriteLine());

            _cache.BatchUpdate(updater => updater.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name1.{0}".FormatWith(i), i)).ToArray()));
            _cache.BatchUpdate(updater => updater.AddOrUpdate(Enumerable.Range(1, 5).Select(i => new Person("Name2.{0}".FormatWith(i), i)).ToArray()));

            abc.Dispose();
        }



        [Test]
        public void ScanTest2()
        {

            var abc = _cache.Connect().ScanCache(0D, (x) =>
            {
                return x.Average(p => p.Age);
            }).Subscribe(result =>
            {
                Console.WriteLine();
            });

            var xxx = _cache.Connect().ScanCache(new PersonStats(0, 0), (current, next) =>
            {
                var avg = next.Average(p => p.Age);
                var count = next.Count();
                return new PersonStats(current.Count + count, avg);
            }).Buffer(3).Subscribe(result => { Console.WriteLine(); });

            var def = _cache.Connect().ScanCache(0, x => x.Count())
                        .Subscribe(result =>
                        {
                            Console.WriteLine();
                        });

            _cache.AddOrUpdate(Enumerable.Range(1, 5).Select(i => new Person("Name1.{0}".FormatWith(i), i)).ToArray());
            _cache.AddOrUpdate(Enumerable.Range(5, 10).Select(i => new Person("Name2.{0}".FormatWith(i), i)).ToArray());
            _cache.Remove(Enumerable.Range(1, 5).Select(i => new Person("Name2.{0}".FormatWith(i), i)).ToArray());
        }

        private class PersonStats
        {
            private readonly int _count;
            private readonly double _averageAge;


            public PersonStats(int count, double averageAge)
            {
                _count = count;
                _averageAge = averageAge;
            }

            public int Count
            {
                get { return _count; }
            }

            public double AverageAge
            {
                get { return _averageAge; }
            }

            public override string ToString()
            {
                return string.Format("Count: {0}, Average: {1}", _count, _averageAge);
            }
        }
    }
}
