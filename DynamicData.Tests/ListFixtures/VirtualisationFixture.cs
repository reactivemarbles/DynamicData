using System.Linq;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class VirtualisationFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private VirtualisingController _controller;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _controller = new VirtualisingController(new VirtualRequest(0,25));
            _results = _source.Connect().Virtualise(_controller).AsAggregator();
   
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

           CollectionAssert.AreEqual(expected, _results.Data.Items);

        }

        [Test]
        public void MoveToNextPage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            _controller.Virualise(new VirtualRequest(25,25));

            var expected = people.Skip(25).Take(25).ToArray();
            CollectionAssert.AreEqual(expected, _results.Data.Items);
        }

        [Test]
        public void InsertAfterPageProducesNothing()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var expected = people.Take(25).ToArray();

            _source.InsertRange(_generator.Take(100),50);
            CollectionAssert.AreEqual(expected, _results.Data.Items);
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

            Assert.AreEqual(newPerson,_results.Data.Items.ElementAt(10));
            Assert.AreEqual(removedPerson, message.Item.Current);
            Assert.AreEqual(ListChangeReason.Remove, message.Reason);
        }

        [Test]
        public void RemoveBeforeShiftsPage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);
            _controller.Virualise(new VirtualRequest(25, 25));
            _source.RemoveAt(0);
            var expected = people.Skip(26).Take(25).ToArray();

            CollectionAssert.AreEqual(expected, _results.Data.Items);


            var removedMessage = _results.Messages[2].ElementAt(0);
            var removedPerson = people.ElementAt(25);
            Assert.AreEqual(removedPerson, removedMessage.Item.Current);
            Assert.AreEqual(ListChangeReason.Remove, removedMessage.Reason);

            var addedMessage = _results.Messages[2].ElementAt(1);
            var addedPerson = people.ElementAt(50);
            Assert.AreEqual(addedPerson, addedMessage.Item.Current);
            Assert.AreEqual(ListChangeReason.Add, addedMessage.Reason);

        }

    }
}