#if SUPPORTS_BINDINGLIST

using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding
{
    public class BindingLIstBindListFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection;

        private readonly RandomPersonGenerator _generator = new();

        private readonly SourceList<Person> _source;

        public BindingLIstBindListFixture()
        {
            _collection = new BindingList<Person>();
            _source = new SourceList<Person>();
            _binder = _source.Connect()
                .AutoRefresh(p => p.Age)
                .Bind(_collection)
                .Subscribe();
        }

        [Test]
        public async Task AddRange()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
            await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
        }

        [Test]
        public async Task AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
            await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
        }

        [Test]
        public async Task Clear()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);
            _source.Clear();
            await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
        }

        [Test]
        public async Task Refresh()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            ListChangedEventArgs? args = null;

            _collection.ListChanged += (_, e) =>
            {
                args = e;
            };

            people[10].Age = 100;

            await Assert.That(args).IsNotNull();
            await Assert.That(args.ListChangedType).IsEqualTo(ListChangedType.ItemChanged);
            await Assert.That(args.NewIndex).IsEqualTo(10);
        }

        [Test]
        public async Task RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);
            _source.Remove(person);

            await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
        }

        [Test]
        public async Task UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.Add(person);
            _source.Replace(person, personUpdated);

            await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
            await Assert.That(_collection.First()).IsEqualTo(personUpdated).Because("Should be updated person");
        }

        public void Dispose()
        {
            _binder.Dispose();
            _source.Dispose();
        }

    }
}
#endif
