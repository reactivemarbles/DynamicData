using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Playground
{
    public class ErrorHandlingFixture
    {
        private class Entity
        {
            public int Key => 10;
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

            public int Key => 10;
        }

        private class ErrorInKey
        {
            public int Key => throw new Exception("Calling Key");
        }

        [Fact]
        public void TransformError()
        {
            bool completed = false;
            bool error = false;

            var cache = new SourceCache<Entity, int>(e => e.Key);

            var subscriber = cache.Connect()
                                  .Transform(e => new TransformEntityWithError(e))
                                  .Finally(() => completed = true)
                                  .Subscribe(updates => { Console.WriteLine(); }, ex => error = true);

            cache.AddOrUpdate(Enumerable.Range(0, 10000).Select(_ => new Entity()).ToArray());
            cache.AddOrUpdate(new Entity());

            subscriber.Dispose();

            error.Should().BeTrue();
            completed.Should().BeTrue();
        }

        [Fact]
        public void FilterError()
        {
            bool completed = false;
            bool error = false;

            var source = new SourceCache<TransformEntityWithError, int>(e => e.Key);

            var subscriber = source.Connect()
                                   .Filter(x => true)
                                   .Finally(() => completed = true)
                                   .Subscribe(updates => { Console.WriteLine(); });

            source.Edit(updater => updater.AddOrUpdate(new TransformEntityWithError(new Entity())), ex => error = true);
            subscriber.Dispose();

            error.Should().BeTrue();
            completed.Should().BeTrue();
        }

        [Fact]
        public void ErrorUpdatingStreamIsHandled()
        {
            bool completed = false;
            bool error = false;

            var cache = new SourceCache<ErrorInKey, int>(p => p.Key);

            var subscriber = cache.Connect().Finally(() => completed = true)
                                  .Subscribe(updates => { Console.WriteLine(); });

            cache.Edit(updater => updater.AddOrUpdate(new ErrorInKey()));
            subscriber.Dispose();

            error.Should().BeTrue();
            completed.Should().BeTrue();
        }


        [Fact]
        public void ExceptionShouldBeThrownInSubscriberCode()
        {
            var changesCountFromSubscriber1 = 0;
            var changesCountFromSubscriber2 = 0;
            var source = new SourceList<int>();

            var observable = source.Connect().RefCount();
            observable.Subscribe(i => changesCountFromSubscriber1++);
            observable.Subscribe(i => { throw new NotImplementedException(); });
            observable.Subscribe(i => changesCountFromSubscriber2++);

            source.Edit(l => l.Add(1));
            source.Edit(l => l.Add(2));

            changesCountFromSubscriber1.Should().Be(2);
            changesCountFromSubscriber2.Should().Be(2);

        }

        [Fact]
        public void SingleSubscriberThatThrows()
        {
            var s = new SourceList<int>();
            s.Connect().Subscribe(i => { throw new NotImplementedException(); });

            Assert.Throws<NotImplementedException>(() => s.Edit(u => u.Add(1)));
        }

        [Fact]
        public void MultipleSubscribersWithOneThrowing()
        {
            var firstSubscriberCallCount = 0;
            var secondSubscriberCallCount = 0;
            var s = new SourceList<int>();
            s.Connect().Subscribe(i => firstSubscriberCallCount++);
            s.Connect().Subscribe(i => { throw new NotImplementedException(); });
            s.Connect().Subscribe(i => secondSubscriberCallCount++);

            s.Edit(u => u.Add(1));

            firstSubscriberCallCount.Should().Be(1);
            secondSubscriberCallCount.Should().Be(1);
        }

        [Fact]
        public void MultipleSubscribersHandlingErrorsWithOneThrowing()
        {
            var firstSubscriberCallCount = 0;
            var secondSubscriberCallCount = 0;
            var firstSubscriberExceptionHandleCount = 0;
            var secondSubscriberExceptionHandleCount = 0;
            var s = new SourceList<int>();
            s.Connect().Subscribe(i => firstSubscriberCallCount++, e => firstSubscriberExceptionHandleCount++);
            s.Connect().Subscribe(i => { throw new NotImplementedException(); },ex=>{});
            s.Connect().Subscribe(i => secondSubscriberCallCount++, e => secondSubscriberExceptionHandleCount++);

            s.Edit(u => u.Add(1));

            firstSubscriberExceptionHandleCount.Should().Be(1);
            secondSubscriberExceptionHandleCount.Should().Be(1);
            firstSubscriberCallCount.Should().Be(1);
            secondSubscriberCallCount.Should().Be(1);
        }

        [Fact]
        public void MultipleSubscribersHandlingErrorsWithOneThrowing_MultipleEdits()
        {
            var firstSubscriberCallCount = 0;
            var secondSubscriberCallCount = 0;
            var firstSubscriberExceptionHandleCount = 0;
            var secondSubscriberExceptionHandleCount = 0;
            var s = new SourceList<int>();
            s.Connect().Subscribe(i => firstSubscriberCallCount++, e => firstSubscriberExceptionHandleCount++);
            s.Connect().Subscribe(i => { throw new NotImplementedException(); });
            s.Connect().Subscribe(i => secondSubscriberCallCount++, e => secondSubscriberExceptionHandleCount++);

            s.Edit(u => u.Add(1));
            s.Edit(u => u.Add(1));


            firstSubscriberCallCount.Should().Be(2);
            secondSubscriberCallCount.Should().Be(2);
            firstSubscriberExceptionHandleCount.Should().Be(1);
            secondSubscriberExceptionHandleCount.Should().Be(1);

        }

        [Fact]
        public void DemonstrateHowErrorArePropagatedWithRxSubject()
        {
            var exceptionsCaughtViaOnError = new List<Exception>();
            Exception exceptionCaughtViaTryCatch = null;
            var subject = new Subject<int>();

            subject.Subscribe(i => Debug.WriteLine(i), e => exceptionsCaughtViaOnError.Add(e));
            subject.Subscribe(i => { throw new NotImplementedException(); }, e => exceptionsCaughtViaOnError.Add(e));
            subject.Subscribe(i => Debug.WriteLine(i), e => exceptionsCaughtViaOnError.Add(e));

            try
            {
                subject.OnNext(1);
            }
            catch (Exception e)
            {
                exceptionCaughtViaTryCatch = e;
            }

            exceptionsCaughtViaOnError.Should().BeEmpty();
            exceptionCaughtViaTryCatch.Should().NotBeNull();
        }
    }
}