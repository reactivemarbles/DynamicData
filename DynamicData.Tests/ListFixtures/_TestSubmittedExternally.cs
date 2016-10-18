using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Annotations;
using DynamicData.Binding;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    public static class DynamicDataExtensions
    {
        public static IObservable<IChangeSet<TObj, TKey>> FilterOnProperty<TObj, TKey, TProp>(this IObservable<IChangeSet<TObj, TKey>> source,
            Expression<Func<TObj, TProp>> selectProp,
            Func<TObj, bool> predicate) where TObj : INotifyPropertyChanged
        {

            return Observable.Create<IChangeSet<TObj, TKey>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = source.Publish();

                //do not filter on initial value otherwise every object loaded will invoke a requery
                var changed = shared.WhenPropertyChanged(selectProp, false)
                    .Select(v => v.Sender);

                //start with predicate to ensure filter on loaded
                var changedMatchFunc = changed.Select(_ => predicate).StartWith(predicate);

                // filter all in source, based on match funcs that update on prop change
                var changedAndMatching = shared.Filter(changedMatchFunc);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }

        public static IObservable<IChangeSet<TObj>> FilterOnProperty<TObj, TProp>(this IObservable<IChangeSet<TObj>> source,
            Expression<Func<TObj, TProp>> selectProp,
            Func<TObj, bool> predicate) where TObj : INotifyPropertyChanged
        {

            return Observable.Create<IChangeSet<TObj>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = source.Publish();

                //do not filter on initial value otherwise every object loaded will invoke a requery
                var changed = shared.WhenPropertyChanged(selectProp, false)
                    .Select(v => v.Sender);

                //start with predicate to ensure filter on loaded
                var changedMatchFunc = changed.Select(_ => predicate).StartWith(predicate);

                // filter all in source, based on match funcs that update on prop change
                var changedAndMatching = shared.Filter(changedMatchFunc);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }
    }
    [TestFixture]
    class _TestSubmittedExternally
    {
        [Test]
        public async Task TestFilterOnProperty()
        {
            var listA = new SourceList<X>();
            var listB = new SourceList<X>();

            using (IObservableList<X> list = listA.Connect()
                                                  .Or(listB.Connect())
                                                  .AsObservableList())
            {
                var nameA1 = "A1";
                var a1 = new X(nameA1);
                var a2 = new X("A2");
                listA.Edit(l =>
                {
                    l.Clear();
                    l.Add(a1);
                    l.Add(a2);
                });

                var b1 = new X("B1");
                var b2 = new X("B2");
                listB.Edit(l =>
                {
                    l.Clear();
                    l.Add(b1);
                    l.Add(b2);
                });

                Assert.AreEqual(4, list.Count);

                int count = await list.CountChanged.FirstAsync();

                Assert.AreEqual(4, count);

                IObservable<IChangeSet<X>> obsFiltered = list.Connect()
                                                             .FilterOnProperty(v => v.IsConnected, v => v.IsConnected);

                using (IObservableList<X> obsFilteredAsList = obsFiltered.AsObservableList())
                {
                    IObservable<IChangeSet<XVm>> obsTransformed = obsFiltered
                        .Transform(v => new XVm(v))
                        .DisposeMany();

                    var ctorCount = 0;
                    var dtorCount = 0;
                    using (IObservableList<XVm> obsTransformedAsList = obsTransformed.AsObservableList())
                    {
                        ctorCount += 4;

                        Assert.That(obsFilteredAsList.Items.Contains(a1));
                        Assert.That(obsFilteredAsList.Items.Count(), Is.EqualTo(obsTransformedAsList.Items.Count()));

                        a1.IsConnected = false;
                        Assert.That(obsFilteredAsList.Items, Has.No.Member(a1));
                        dtorCount++;
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                        a1.IsConnected = true;
                        Assert.That(obsFilteredAsList.Items, Has.Member(a1));
                        ctorCount++;
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                        //Console.WriteLine("--remove");
                        listA.Remove(a1);
                        dtorCount++;
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                      //  Console.WriteLine("--add");
                        listA.Add(a1);
                        ctorCount++;
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                       // Console.WriteLine("--clear");
                        listA.Clear();
                        dtorCount += 2; //FIX:  List A contains 2 items (was adding 4)
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                     //   Console.WriteLine("--add");

                        //FIX: Maybe a debate required!  List B already contains b1, so not regarded as a new item in the Or result
                        Debug.Assert(listB.Items.Contains(b1));
                        listA.Add(b1);
                        //  ctorCount++;
                        Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                        Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));

                    //    Console.WriteLine("--disp");
                    }
                    dtorCount += 2; //FIX: Should be +3 s this is what 
                    Assert.That(XVm.Constructed, Is.EqualTo(ctorCount));
                    Assert.That(XVm.Destructed, Is.EqualTo(dtorCount));
                }
            }
        }
    }

    public class XVm : IDisposable, IEquatable<XVm>
    {
        public static volatile int Constructed;
        public static volatile int Destructed;
        private readonly X _v;

        public XVm(X v)
        {
            _v = v;
            Constructed++;
            //Console.WriteLine("{0} VM.ctor", _v);
        }

        public void Dispose()
        {
            Destructed++;
            // Console.WriteLine("{0} VM.disp", _v);
        }

        public bool Equals(XVm other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Equals(_v, other._v);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((XVm)obj);
        }

        public override int GetHashCode()
        {
            return _v?.GetHashCode() ?? 0;
        }
    }

    public class X : AbstractNotifyPropertyChanged, IEquatable<X>
    {
        private bool _isConnected;
        private string _name;

        public X(string name)
        {
            _name = name;
            _isConnected = true;
        }

        [PublicAPI]
        public string Name { get { return _name; } set { this.SetAndRaise(ref _name, value); } }

        [PublicAPI]
        public bool IsConnected { get { return _isConnected; } set { this.SetAndRaise(ref _isConnected, value); } }

        public bool Equals(X other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(_name, other._name) && _isConnected == other._isConnected;
        }

        public override string ToString()
        {
            return $"{Name} {IsConnected}";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((X)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_name?.GetHashCode() ?? 0) * 397) ^ _isConnected.GetHashCode();
            }
        }
    }
}
