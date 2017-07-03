using DynamicData.Tests.Domain;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class DistinctValuesFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void FiresAddWhenaNewItemIsAdded()
        {
            _source.Add(new Person("Person1", 20));

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void FiresBatchResultOnce()
        {
            _source.Edit(list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person2", 21));
                list.Add(new Person("Person3", 22));
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(3, "Should be 3 items in the cache");

            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {20, 21, 22});
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void DuplicatedResultsResultInNoAdditionalMessage()
        {
            _source.Edit(list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 update message");
            _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void RemovingAnItemRemovesTheDistinct()
        {
            var person = new Person("Person1", 20);

            _source.Add(person);
            _source.Remove(person);
            _results.Messages.Count.Should().Be(2, "Should be 1 update message");
            _results.Data.Count.Should().Be(0, "Should be 1 items in the cache");

            _results.Messages.First().Adds.Should().Be(1, "First message should be an add");
            _results.Messages.Skip(1).First().Removes.Should().Be(1, "Second messsage should be a remove");
        }

        [Test]
        public void Replacing()
        {
            var person = new Person("A", 20);
            var replaceWith = new Person("A", 21);

            _source.Add(person);
            _source.Replace(person, replaceWith);
            _results.Messages.Count.Should().Be(2, "Should be 1 update message");
            _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");

            _results.Messages.First().Adds.Should().Be(1, "First message should be an add");
            _results.Messages.Skip(1).First().Count.Should().Be(2, "Second messsage should be an add an a remove");
        }
    }
}
