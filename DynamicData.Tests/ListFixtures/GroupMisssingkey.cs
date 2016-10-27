using System;
using System.Threading;
using System.Timers;
using NUnit.Framework;
using Timer = System.Timers.Timer;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class GroupMisssingkey
    {
        private DataSource _dataSource;

        [Explicit]
        [Test]
        public void Test2()
        {
            //This causes a race condition!
            _dataSource = new DataSource();
            var timer1 = new Timer { Interval = 10 };
            timer1.Elapsed += TimerElapsed;
            timer1.Start();

            var timer2 = new Timer { Interval = 10 };
            timer2.Elapsed += TimerElapsed;
            timer2.Start();

            Thread.Sleep(TimeSpan.FromSeconds(120));
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            var r = new Random();
            var item = new MyItem { ID = r.Next(0, 100) };
            _dataSource.Add(item);
        }

        private class DataSource
        {
            private readonly object _locker = new object();
            private readonly SourceCache<MyItem, int> _sourceCache;
            public DataSource()
            {
                _sourceCache = new SourceCache<MyItem, int>(k => k.ID);
                _sourceCache
                    .Connect()
                    .Group(g => g.TypeID)
                    .Subscribe(n => { });
            }

            public void Add(MyItem item)
            {
                //lock (locker) // uncomment to get rid of MissingKeyException
                {
                    _sourceCache.Edit(
                        updater => updater.AddOrUpdate(item),
                        error => { Console.WriteLine(error); });
                }
            }
        }

        private class MyItem : IEquatable<MyItem>
        {
            public int ID { get; set; }
            public int TypeID { get; set; }

            public bool Equals(MyItem other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ID == other.ID && TypeID == other.TypeID;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MyItem)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = ID;
                    hashCode = (hashCode * 397) ^ TypeID;
                    return hashCode;
                }
            }

            public static bool operator ==(MyItem left, MyItem right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(MyItem left, MyItem right)
            {
                return !Equals(left, right);
            }
        }
    }
}