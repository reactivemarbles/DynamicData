using System.Linq;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class PageFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private PageController _controller;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _controller = new PageController(new PageRequest(1, 25));
            _results = _source.Connect().Page(_controller).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _controller.Dispose();
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void VirtualiseInitial()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            var expected = people.Take(25).ToArray();
            _results.Data.Items.ShouldAllBeEquivalentTo(expected);
        }

        [Test]
        public void MoveToNextPage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            _controller.Change(new PageRequest(2, 25));

            var expected = people.Skip(25).Take(25).ToArray();
            _results.Data.Items.ShouldAllBeEquivalentTo(expected);
        }

        [Test]
        public void InsertAfterPageProducesNothing()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            var expected = people.Take(25).ToArray();

            _source.InsertRange(_generator.Take(100), 50);
            _results.Data.Items.ShouldAllBeEquivalentTo(expected);
        }

        [Test]
        public void InsertInPageReflectsChange()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var newPerson = new Person("A", 1);
            _source.Insert(10, newPerson);

            var message = _results.Messages[1].ElementAt(0);
            var removedPerson = people.ElementAt(24);

            _results.Data.Items.ElementAt(10).Should().Be(newPerson);
            message.Item.Current.Should().Be(removedPerson);
            message.Reason.Should().Be(ListChangeReason.Remove);
        }

        [Test]
        public void RemoveBeforeShiftsPage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            _controller.Change(new PageRequest(2, 25));
            _source.RemoveAt(0);
            var expected = people.Skip(26).Take(25).ToArray();

            _results.Data.Items.ShouldAllBeEquivalentTo(expected);

            var removedMessage = _results.Messages[2].ElementAt(0);
            var removedPerson = people.ElementAt(25);
            removedMessage.Item.Current.Should().Be(removedPerson);
            removedMessage.Reason.Should().Be(ListChangeReason.Remove);

            var addedMessage = _results.Messages[2].ElementAt(1);
            var addedPerson = people.ElementAt(50);
            addedMessage.Item.Current.Should().Be(addedPerson);
            addedMessage.Reason.Should().Be(ListChangeReason.Add);
        }

        [Test]
        public void MoveWithinSamePage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            var personToMove = people[0];
            _source.Move(0, 10);

            var actualPersonAtIndex10 = _results.Data.Items.ElementAt(10);
            actualPersonAtIndex10.Should().Be(personToMove);
        }

        [Test]
        public void MoveWithinSamePage2()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            var personToMove = people[10];
            _source.Move(10, 0);

            var actualPersonAtIndex0 = _results.Data.Items.ElementAt(0);
            actualPersonAtIndex0.Should().Be(personToMove);
        }
    }
}
