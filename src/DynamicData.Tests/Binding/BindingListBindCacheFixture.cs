#if SUPPORTS_BINDINGLIST

using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding
{
    public class BindingListCacheFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection = new();

        private readonly RandomPersonGenerator _generator = new();

        private readonly ISourceCache<Person, string> _source;

        public BindingListCacheFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect().Bind(_collection).Subscribe();
        }

        [Test]
        public async Task AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
            await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
        }

        [Test]
        public async Task BatchAdd()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
            await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
        }

        [Test]
        public async Task BatchRemove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);
            _source.Clear();
            await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
        }

        [Test]
        public async Task RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);
            _source.Remove(person);

            await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
        }

        [Test]
        public async Task Refresh()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            ListChangedEventArgs? args = null;

            _collection.ListChanged += (_, e) =>
            {
                args = e;
            };

            _source.Refresh(people[10]);

            await Assert.That(args).IsNotNull();
            await Assert.That(args.ListChangedType).IsEqualTo(ListChangedType.ItemChanged);
            await Assert.That(args.NewIndex).IsEqualTo(10);
        }

        [Test]
        public async Task UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(personUpdated);

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
