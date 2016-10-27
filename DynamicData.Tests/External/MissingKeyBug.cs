using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace DynamicData.Tests.External
{

    [TestFixture]
    public sealed  class MissingKeyBug 
    {
        private  SourceCache<TestObject, int> m_sourceCache;
        private  ReplaySubject<Func<TestObject, bool>> m_changeObs;
        private  ReadOnlyObservableCollection<GroupContainer> m_entries;
        public ReadOnlyObservableCollection<GroupContainer> Entries => m_entries;


        [Test]
        public void  ThrowsExceptionInGroup()
        {
            var scheduler = new TestScheduler();

         //   this.InitializeComponent();

            m_changeObs = new ReplaySubject<Func<TestObject, bool>>(1);

            var dispatcher = Scheduler.Immediate;
            Exception error = null;

            m_sourceCache = new SourceCache<TestObject, int>(x => x.Id);
            m_sourceCache.Connect()
                .Filter(m_changeObs)
               // .Do(x=> Console.WriteLine(x))
                .Group(x => x.Group)
                .Transform(x => new GroupContainer(x, dispatcher))
               // .ObserveOn(dispatcher)
                .Bind(out m_entries)
                .Subscribe(x => { }, ex => error = ex);

            var xxx =   Observable.Interval(TimeSpan.FromMilliseconds(250), scheduler)
               .Subscribe(x => DoFilterAndAdd());


            scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);

            Assert.IsNull(error);

            //DoFilterAndAdd();
            //Thread.Sleep(1000);
        }

        private int m_count;
        private void AddItems()
        {
            m_sourceCache.Edit(innerCache =>
            {
                var list = new List<TestObject>();
                for (var i = 0; i < 10; i++)
                {
                    m_count++;
                    list.Add(new TestObject() { Id = m_count, Group = i, Text = DateTime.Now.ToString("T") });
                }
                innerCache.AddOrUpdate(list);
            });
        }

        private bool m_bool;
        private void DoFilterAndAdd()
        {
            m_bool = !m_bool;
            m_changeObs.OnNext(o => m_bool);
            AddItems();
        }
    }

    public class TestObject : IEquatable<TestObject>
    {
        public int Id { get; set; }

        public string Text { get; set; }

        public int Group { get; set; }


        public bool Equals(TestObject other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TestObject) obj);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(TestObject left, TestObject right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TestObject left, TestObject right)
        {
            return !Equals(left, right);
        }

        private sealed class IdTextGroupEqualityComparer : IEqualityComparer<TestObject>
        {
            public bool Equals(TestObject x, TestObject y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.Id == y.Id && string.Equals(x.Text, y.Text) && x.Group == y.Group;
            }

            public int GetHashCode(TestObject obj)
            {
                unchecked
                {
                    var hashCode = obj.Id;
                    hashCode = (hashCode*397) ^ (obj.Text != null ? obj.Text.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ obj.Group;
                    return hashCode;
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Text)}: {Text}, {nameof(Group)}: {Group}";
        }
    }

    public class GroupContainer
    {
        private readonly ReadOnlyObservableCollection<GroupEntry> m_entries;

        public ReadOnlyObservableCollection<GroupEntry> Entries => m_entries;

        public GroupContainer(IGroup<TestObject, int, int> groupEntries, IScheduler scheduler)
        {
            groupEntries.Cache.Connect()
                                    .Transform(x => (new GroupEntry(x)))
                                    .ObserveOn(scheduler)
                                    .Bind(out m_entries)
                                    .Subscribe();
            Title = groupEntries.Key.ToString("D");
        }
        public string Title { get; set; }

    }

    public class GroupEntry
    {
        public string Title { get; set; }

        public GroupEntry(TestObject obj)
        {
            Title = obj.Text;
        }
    }
}
