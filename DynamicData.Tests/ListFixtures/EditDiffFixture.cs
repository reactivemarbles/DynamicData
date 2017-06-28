using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class EditDiffFixture
    {
        private SourceList<Person> _cache;
        private ChangeSetAggregator<Person> _result;

        [SetUp]
        public void Initialise()
        {
            _cache = new SourceList<Person>();
            _result = _cache.Connect().AsAggregator();
            _cache.AddRange(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
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

            _cache.EditDiff(newPeople, Person.NameAgeGenderComparer);

            _cache.Count.Should().Be(15);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
        }

        [Test]
        public void EditWithSameData()
        {
            var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, Person.NameAgeGenderComparer);

            _cache.Count.Should().Be(10);
            _cache.Items.ShouldAllBeEquivalentTo(newPeople);
            _result.Messages.Count.Should().Be(1);
        }

        [Test]
        public void Amends()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            _cache.Count.Should().Be(3);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(3);
            lastChange.Removes.Should().Be(10);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }

        [Test]
        public void Removes()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            _cache.Count.Should().Be(7);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(0);
            lastChange.Removes.Should().Be(3);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }


        [Test]
        public void VariousChanges()
        {
            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            _cache.Count.Should().Be(10);

            var lastChange = _result.Messages.Last();
            lastChange.Adds.Should().Be(5);
            lastChange.Removes.Should().Be(5);

            _cache.Items.ShouldAllBeEquivalentTo(newList);
        }
    }
}
