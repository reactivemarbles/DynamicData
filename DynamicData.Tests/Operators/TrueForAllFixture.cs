using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class TrueForAllFixture
    {
        private ISourceCache<ObjectWithObservable, int> _source;
        private IObservable<bool> _observable;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
            _observable = _source.Connect().TrueForAll(o => o.Observable, o => o == true);

        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void ItemAddedDoesNotReturnTrueUntilTheObservableHasAValue()
        {
            bool invoked = false;

            var subscribed = _observable.Subscribe(result =>
            {
                invoked = true;
            });


            var item = new ObjectWithObservable(1);
           // item.InvokeObservable(true);
            _source.AddOrUpdate(item);

            Assert.IsTrue(invoked,"S");
            item.InvokeObservable(true);


            _source.AddOrUpdate(new ObjectWithObservable(2));
            subscribed.Dispose();
        }





    private class ObjectWithObservable
        {
            private readonly int _id;
            private readonly ISubject<bool> _changed = new Subject<bool>();
            private bool _value;

            public ObjectWithObservable(int id)
            {
                _id = id;
            }

            public void InvokeObservable(bool value)
            {
                _value = value;
                _changed.OnNext(value);
            }

            public IObservable<bool> Observable
            {
                get { return _changed; }
            }

        public bool Value
        {
            get { return _value; }
        }

            public int Id
            {
                get { return _id; }
            }
        }

    }
}