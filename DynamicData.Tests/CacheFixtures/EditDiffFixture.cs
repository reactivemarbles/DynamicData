
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    [TestFixture]
    public class EditDiffFixture
    {
        private SourceCache<Person, string> _cache;
        private ChangeSetAggregator<Person, string> _result;

        [SetUp]
        public void Initialise()
        {
            _cache = new SourceCache<Person, string>(p => p.Name);
            _result = _cache.Connect().AsAggregator();
            _cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
        }

        [TearDown]
        public void OnTestCompleted()
        {
            _cache.Dispose();
            _result.Dispose();
        }



        [Test]
        public void New()
        {
            var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

            _cache.Count.Should().Be(15);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
        }

        [Test]
        public void EditWithSameData()
        {
            var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

            _cache.Count.Should().Be(10);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            _result.Messages.Count.Should().Be(1);
        }

        [Test]
        public void Amends()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            _cache.Count.Should().Be(3);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(0);
            lastChange.Updates.Should().Be(3);
            lastChange.Removes.Should().Be(7);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }

        [Test]
        public void Removes()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            _cache.Count.Should().Be(7);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(0);
            lastChange.Updates.Should().Be(0);
            lastChange.Removes.Should().Be(3);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }


        [Test]
        public void VariousChanges()
        {
            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            _cache.Count.Should().Be(10);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
            lastChange.Updates.Should().Be(5);
            lastChange.Removes.Should().Be(5);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }

        [Test]
        public void New_WithEqualityComparer()
        {
            var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, Person.AgeComparer);

            _cache.Count.Should().Be(15);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
        }

        [Test]
        public void EditWithSameData_WithEqualityComparer()
        {
            var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, Person.AgeComparer);

            _cache.Count.Should().Be(10);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            var lastChange = _result.Messages.Last();
            _result.Messages.Count.Should().Be(1);
        }

        [Test]
        public void Amends_WithEqualityComparer()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, Person.AgeComparer);

            _cache.Count.Should().Be(3);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(0);
            lastChange.Updates.Should().Be(3);
            lastChange.Removes.Should().Be(7);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }

        [Test]
        public void Removes_WithEqualityComparer()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, Person.AgeComparer);

            _cache.Count.Should().Be(7);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(0);
            lastChange.Updates.Should().Be(0);
            lastChange.Removes.Should().Be(3);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }


        [Test]
        public void VariousChanges_WithEqualityComparer()
        {
            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

            _cache.EditDiff(newList, Person.AgeComparer);

            _cache.Count.Should().Be(10);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
            lastChange.Updates.Should().Be(5);
            lastChange.Removes.Should().Be(5);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }
    }
}
