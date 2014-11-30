#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Controllers;
using DynamicData.Kernel;
using DynamicData.Operators;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

#endregion

namespace DynamicData.Tests.Performance
{
    [TestFixture(Ignore = true,Description = "Benchmarking")]
    internal class PerformanceMeasures
    {
        private ISourceCache<Person, string> _source;
        private IObservable<IChangeSet<Person, string>> _stream;

        [TearDown]
        public void CleanUp()
        {
            if (_source != null)
                _source.Dispose();
        }

        public void SetStream()
        {
            _source = new SourceCache<Person, string>(p=>p.Name);
            _stream = _source.Connect();
        }



        private void FilterUpdate(int number, ParallelisationOptions options)
        {
            SetStream();
            var items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i));
            _stream.Filter(p => p.Age > number/2, options)
                   .Subscribe(u => { });

            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} updates with {1}".FormatWith(number, options.Type));
        }
        
 
        private void StreamOnly(int number)
        {
            SetStream();
            IEnumerable<Person> items = Enumerable.Range(1, number)
                                                  .Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} stream only".FormatWith(number));
        }


        private void Transform(int number, ParallelisationOptions options)
        {
            SetStream();
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _stream.Transform((p) => new PersonWithGender(p.Name, p.Age, "M"), options).Subscribe(u => { });

            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} updates with {1}".FormatWith(number, options.Type));
        }




        private void Virtualise(int number)
        {
            var virtualiser = new VirtualisingController(new VirtualRequest(0, 25));
            SetStream();
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _stream.Sort(new PersonSorter()).Virtualise(virtualiser).Subscribe(u => { });
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} sort and virtualise".FormatWith(number));
        }

        private void Sort(int number)
        {
            Person[] items = Enumerable.Range(1, number)
                .Select(i => new Person("Name.{0}".FormatWith(i), i))
               // .OrderBy(p=>Guid.NewGuid())
                .ToArray();

            SetStream();
            _stream.Sort(new PersonSorter()).Subscribe(u => { });
            Timer.ToConsole(() => { _source.BatchUpdate(updater => updater.AddOrUpdate(items)); }, 1,
                            "{0} sort".FormatWith(number));
        }

        private void Group(int number, ParallelisationOptions options)
        {
            SetStream();
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _stream.Group(p => p.Age / 100).Subscribe(u => { });

            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} group".FormatWith(number));
        }

        private void GroupAndSubscribe(int number)
        {
            SetStream();
            IEnumerable<IDisposable> disposables = Enumerable.Empty<IDisposable>();
            Person[] items = Enumerable.Range(100, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _stream.Group(p => p.Age/10).Subscribe(u =>
                {
                    disposables = u.Select(update => update.Current.Cache.Connect().Subscribe());
                });

            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} GroupAndSubscribe".FormatWith(number));

            disposables.ForEach(d=>d.Dispose());
        }

        private void FilterAndTransform(int number, ParallelisationOptions options)
        {
            SetStream();
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _stream.Filter(p => p.Age > number/2, options)
                   .Transform(p => new PersonWithGender(p.Name, p.Age, "M"), options)
                   .Subscribe(u => { });

            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(items)), 1,
                            "{0} updates with {1}".FormatWith(number, options.Type));
        }

        private void ConnectToStream(int number)
        {
            var source = new SourceCache<Person, string>(p=>p.Key);

            Person[] items = Enumerable.Range(1, 1000).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            source.BatchUpdate(updater => updater.AddOrUpdate(items));

            Timer.ToConsole(() => source.Connect().Subscribe(u => { }), number,
                            "{0} connections to stream".FormatWith(number));
        }

        private void DataQuery(int number)
        {            
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            var source = new SourceCache<Person, string>(p=>p.Key);

            source.BatchUpdate(updater => updater.AddOrUpdate(items));
            
            Timer.ToConsole(() =>
                                {
                                    source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Sample", 10)));
                                   
                                }
                            , 100, "Query with {0} items (1 subscriber)".FormatWith(number));

            source.Dispose();
        }


        private void ObservableCache(int number)
        {
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            var source = new SourceCache<Person, string>(p=>p.Key);
            source.BatchUpdate(updater => updater.AddOrUpdate(items));

            Timer.ToConsole(() =>
            {

                source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Sample", 10)));
            }
                            , 1, "ObservableCache with {0} items (1 subscriber)".FormatWith(number));
        }


        private void UpdateStreamWithMultipleConnections(int numberofconnections)
        {
            var source = new SourceCache<Person, string>(p=>p.Name);

            Person[] items = Enumerable.Range(1, 1000).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            source.BatchUpdate(updater => updater.AddOrUpdate(items));
            IDisposable[] xxx = Enumerable.Range(1, numberofconnections)
                                          .Select(_ => source.Connect().Subscribe(u => { }))
                                          .ToArray();


            Timer.ToConsole(() => source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Someone", 10))), 1,
                            "1 update with {0} connections to stream".FormatWith(numberofconnections));

            xxx.ForEach(x => x.Dispose());
        }

        //private void MutlipleThreadStreamRead(int numberofconnections)
        //{
        //    Console.WriteLine("-----");
        //   // var taksFactory = new TaskPoolScheduler(Task.Factory);
        //    var source = new Streamsource<Person, string>(p => p.Name);

        //    Person[] items = Enumerable.Range(1, 1000).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

        //    source.Change(updater => updater.AddOrUpdate(items));

        //    IEnumerable<IDisposable> xxx = Enumerable.Range(1, numberofconnections)
        //        .Select(_ =>
        //            {
        //                "Subscribing".ToConsoleWithThreadId();
        //                return source.Connect()
        //                    //.Filter(p=>p.Age>250)
        //                    //.Transform(p=>new PersonWithGender(p.Name,p.Age,"X"))
        //                    .Subscribe(u => { "{1}: {0} transformd received".FormatWith(u.Count,_).ToConsoleWithThreadId(); });
        //            })
        //        .ToArray();


        //    Person[] updates = Enumerable.Range(1000, 50).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

        //    source.Change(updater =>
        //        {
        //            updater.AddOrUpdate(updates);
        //        });


        //    //Timer.ToConsole(() => ), 1, "{1} update with {0} connections to stream".FormatWith(numberofconnections,updates.Length));

        //    Thread.Sleep(1000);
        //    xxx.ForEach(x => x.Dispose());
        //}

        private void ComparerSort(int number)
        {
          
            var cache = new Cache<Person, string>();
            Person[] items = Enumerable.Range(1, number)
                .Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            Timer.ToConsole(() => { items.ForEach(i => cache.AddOrUpdate(i, i.Name)); }, 1,
                            "{0} cache updates".FormatWith(number));
        }

        private void UpdateCache(int number)
        {
            var cache = new Cache<Person, string>();
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            Timer.ToConsole(() => { items.ForEach(i => cache.AddOrUpdate(i, i.Name)); }, 1,
                            "{0} cache updates".FormatWith(number));
        }



        private void UpdateCacheWithUpdates(int number)
        {
            var cache = new Cache<Person, string>();
            var cacheUpdater = new IntermediateUpdater<Person, string>(cache);
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            Timer.ToConsole(() => { items.ForEach(i => cacheUpdater.AddOrUpdate(i, i.Name)); }, 1,
                            "{0} cache updates".FormatWith(number));
        }


        private void UpdateCacheWithChangeSet(int number)
        {
            var cache = new Cache<Person, string>();
            var cacheUpdater = new IntermediateUpdater<Person, string>(cache);
            Person[] items = Enumerable.Range(1, number).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            Timer.ToConsole(() =>
                {
                    items.ForEach(i => cacheUpdater.AddOrUpdate(i, i.Name));
                    IChangeSet<Person, string> collection = cacheUpdater.AsChangeSet();
                }, 1, "{0} cache updates".FormatWith(number));
        }

        [Test]
        public void CacheUpdate()
        {
            UpdateCache(1);
            UpdateCache(1);
            UpdateCache(10);
            UpdateCache(100);
            UpdateCache(1000);
            UpdateCache(10000);
            UpdateCache(100000);
        }

        [Test]
        public void CacheUpdater()
        {
            UpdateCacheWithUpdates(1);
            UpdateCacheWithUpdates(1);
            UpdateCacheWithUpdates(10);
            UpdateCacheWithUpdates(100);
            UpdateCacheWithUpdates(1000);
            UpdateCacheWithUpdates(10000);
            UpdateCacheWithUpdates(100000);
        }

        [Test]
        public void CacheUpdaterWithCollection()
        {
            UpdateCacheWithChangeSet(1);
            UpdateCacheWithChangeSet(1);
            UpdateCacheWithChangeSet(10);
            UpdateCacheWithChangeSet(100);
            UpdateCacheWithChangeSet(1000);
            UpdateCacheWithChangeSet(10000);
            UpdateCacheWithChangeSet(100000);
        }

        [Test]
        public void ConnectToStream()
        {
            ConnectToStream(1);
            ConnectToStream(1);
            ConnectToStream(10);
            ConnectToStream(100);
            ConnectToStream(1000);
            ConnectToStream(10000);
        }

        [Test]
        public void DataQuery()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                DataQuery((int) value);
            }
        }


        [Test]
        public void ObservableCache()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                ObservableCache((int)value);
            }
        }

        [Test]
        public void FilterAndTransform()
        {
            FilterAndTransform(1, new ParallelisationOptions(ParallelType.None));
            FilterAndTransform(10, new ParallelisationOptions(ParallelType.None));
            FilterAndTransform(100, new ParallelisationOptions(ParallelType.None));
            FilterAndTransform(1000, new ParallelisationOptions(ParallelType.None));
            FilterAndTransform(10000, new ParallelisationOptions(ParallelType.None));
            FilterAndTransform(100000, new ParallelisationOptions(ParallelType.None));
            Console.WriteLine();

            FilterAndTransform(1, new ParallelisationOptions(ParallelType.Parallelise));
            FilterAndTransform(10, new ParallelisationOptions(ParallelType.Parallelise));
            FilterAndTransform(100, new ParallelisationOptions(ParallelType.Parallelise));
            FilterAndTransform(1000, new ParallelisationOptions(ParallelType.Parallelise));
            FilterAndTransform(10000, new ParallelisationOptions(ParallelType.Parallelise));
            FilterAndTransform(100000, new ParallelisationOptions(ParallelType.Parallelise));

            Console.WriteLine();
            FilterAndTransform(1, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterAndTransform(10, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterAndTransform(100, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterAndTransform(1000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterAndTransform(10000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterAndTransform(100000, new ParallelisationOptions(ParallelType.ParalledOrdered));
        }

        [Test]
        public void FilterMeasures()
        {
            FilterUpdate(1, new ParallelisationOptions(ParallelType.None));
            FilterUpdate(10, new ParallelisationOptions(ParallelType.None));
            FilterUpdate(100, new ParallelisationOptions(ParallelType.None));
            FilterUpdate(1000, new ParallelisationOptions(ParallelType.None));
            FilterUpdate(10000, new ParallelisationOptions(ParallelType.None));
            FilterUpdate(100000, new ParallelisationOptions(ParallelType.None));
            Console.WriteLine();

            FilterUpdate(1, new ParallelisationOptions(ParallelType.Parallelise));
            FilterUpdate(10, new ParallelisationOptions(ParallelType.Parallelise));
            FilterUpdate(100, new ParallelisationOptions(ParallelType.Parallelise));
            FilterUpdate(1000, new ParallelisationOptions(ParallelType.Parallelise));
            FilterUpdate(10000, new ParallelisationOptions(ParallelType.Parallelise));
            FilterUpdate(100000, new ParallelisationOptions(ParallelType.Parallelise));
            Console.WriteLine();
            FilterUpdate(10, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterUpdate(100, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterUpdate(1000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterUpdate(10000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            FilterUpdate(100000, new ParallelisationOptions(ParallelType.ParalledOrdered));
        }

        [Test]
        public void Group()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                Group((int) value, new ParallelisationOptions(ParallelType.None));
            }
            Console.WriteLine();

            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                Group((int) value, new ParallelisationOptions(ParallelType.Parallelise));
            }
            Console.WriteLine();


            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                Group((int) value, new ParallelisationOptions(ParallelType.ParalledOrdered));
            }
        }

        [Test]
        public void GroupAndSubscribe()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                GroupAndSubscribe((int) value);
            }
            Console.WriteLine();

            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                GroupAndSubscribe((int) value);
            }
            Console.WriteLine();


            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                GroupAndSubscribe((int) value);
            }
        }

        [Test]
        public void Sort()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                Sort((int) value);
            }
            Sort(5000);
        }

        [Test]
        public void SortAndVirtualise()
        {
            for (int i = 0; i < 6; i++)
            {
                double value = i == 0 ? 1 : Math.Pow(10, i);
                Virtualise((int) value);
            }
        }

        [Test]
        public void StreamsourceSubscriptionTests()
        {
            var obs = new ObservableCache<Person, string>();
            var subscriptions = new List<IDisposable>();
            Timer.ToConsole(() => { subscriptions.Add(obs.Connect().Subscribe(u => { })); }, 1000,
                            "Time to subscribe to a subject");

            subscriptions.ForEach(s => s.Dispose());
        }

        [Test]
        public void StreamOnly()
        {
            StreamOnly(1);
            StreamOnly(1);
            StreamOnly(10);
            StreamOnly(100);
            StreamOnly(1000);
            StreamOnly(10000);
            StreamOnly(100000);
        }

        [Test]
        public void SubjectOnly()
        {
            IObservable<int> obs = Observable.Create<int>(observer =>
                {
                    observer.OnNext(1);
                    return Disposable.Empty;
                });

            var subscriptions = new List<IDisposable>();
            //var subject = new Subject<int>();
            Timer.ToConsole(() => { subscriptions.Add(obs.Subscribe(u => { })); }, 10000,
                            "Time to subscribe to a subject");

            subscriptions.ForEach(s => s.Dispose());
        }

        [Test]
        public void Transform()
        {
            Transform(1, new ParallelisationOptions(ParallelType.None));
            Transform(10, new ParallelisationOptions(ParallelType.None));
            Transform(100, new ParallelisationOptions(ParallelType.None));
            Transform(1000, new ParallelisationOptions(ParallelType.None));
            Transform(10000, new ParallelisationOptions(ParallelType.None));
            Transform(100000, new ParallelisationOptions(ParallelType.None));
            Console.WriteLine();

            Transform(1, new ParallelisationOptions(ParallelType.Parallelise));
            Transform(10, new ParallelisationOptions(ParallelType.Parallelise));
            Transform(100, new ParallelisationOptions(ParallelType.Parallelise));
            Transform(1000, new ParallelisationOptions(ParallelType.Parallelise));
            Transform(10000, new ParallelisationOptions(ParallelType.Parallelise));
            Transform(100000, new ParallelisationOptions(ParallelType.Parallelise));

            Console.WriteLine();
            Transform(1, new ParallelisationOptions(ParallelType.ParalledOrdered));
            Transform(10, new ParallelisationOptions(ParallelType.ParalledOrdered));
            Transform(100, new ParallelisationOptions(ParallelType.ParalledOrdered));
            Transform(1000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            Transform(10000, new ParallelisationOptions(ParallelType.ParalledOrdered));
            Transform(100000, new ParallelisationOptions(ParallelType.ParalledOrdered));
        }

        [Test]
        public void UpdateStreamWithMultipleConnections()
        {
            UpdateStreamWithMultipleConnections(1);
            UpdateStreamWithMultipleConnections(1);
            UpdateStreamWithMultipleConnections(10);
            UpdateStreamWithMultipleConnections(100);
            UpdateStreamWithMultipleConnections(1000);
            UpdateStreamWithMultipleConnections(10000);
        }
    }
}