using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Kernel;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    public  class ErrorHandlingFixture
    {
        [SetUp]
        public void Initialise()
        {

        }
        
        private class Entity 
        {

            public int Key
            {
                get
                {
                    return   10;
                }
            }
        }

        private class TransformEntityWithError
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public TransformEntityWithError(Entity entity)
            {
                throw new Exception("Error transforming entity");
            }


            public int Key
            {
                get { return 10; }
            }
        }

        private class ErrorInKey
        {

            public int Key
            {
                get
                {
                    throw new Exception("Calling Key");
                }
            }
        }

        [Test]
        public void SubscribeError()
        {

            bool completed = false;
            bool error = false;

            var feeder = new SourceCache<Entity, int>(e=>e.Key);

            var subscriber = feeder.Connect()
                                 .Finally(() => completed = true)
                                 .SubscribeAndCatch( updates => { throw new Exception("Dodgy"); }, ex => error = true);

            feeder.BatchUpdate(updater => updater.AddOrUpdate(new Entity()));


            subscriber.Dispose();

            Assert.IsTrue(error, "Error has not been invoked");
            Assert.IsTrue(completed, "Completed has not been called");
        }

        [Test]
        public void TransformError()
        {

            bool completed = false;
            bool error = false;


            var feeder = new SourceCache<Entity, int>(e => e.Key);

            var subscriber = feeder.Connect()
                            .Transform(e =>
                                {
                                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                                    Thread.Sleep(TimeSpan.FromSeconds(1));
                                    return new TransformEntityWithError(e);
                                })

                            .Finally(() => completed = true)
                            .Subscribe(updates => { Console.WriteLine(); }, ex => error = true);

            feeder.BatchUpdate(updater => updater.AddOrUpdate(Enumerable.Range(0, 10000).Select(_ => new Entity()).ToArray()));
            feeder.BatchUpdate(updater => updater.AddOrUpdate(new Entity()));


            subscriber.Dispose();

            Assert.IsTrue(error, "Error has not been invoked");
            Assert.IsTrue(completed, "Completed has not been called");
        }

        [Test]
        public void FilterError()
        {

            bool completed = false;
            bool error = false;

            var feeder = new SourceCache<TransformEntityWithError, int>(e=>e.Key);

            var subscriber = feeder.Connect()
                            .Filter(x=>true)
                            .Finally(() => completed = true)
                            .Subscribe(updates => { Console.WriteLine(); }, ex => error = true);

            feeder.BatchUpdate(updater => updater.AddOrUpdate(new TransformEntityWithError(new Entity())));
            subscriber.Dispose();

            Assert.IsTrue(error, "Error has not been invoked");
            Assert.IsTrue(completed, "Completed has not been called");
        }


        [Test]
        public void ErrorUpdatingStreamIsHandled()
        {

            bool completed = false;
            bool error = false;

            var feeder = new SourceCache<ErrorInKey, int>(p=>p.Key);
 
            var subscriber = feeder.Connect().Finally(() => completed = true)
                                 .Subscribe(updates => { Console.WriteLine(); }, ex => error = true);

            feeder.BatchUpdate(updater => updater.AddOrUpdate(new ErrorInKey()));
            subscriber.Dispose();

            Assert.IsTrue(error, "Error has not been invoked");
            Assert.IsTrue(completed, "Completed has not been called");
        }

    }
}
