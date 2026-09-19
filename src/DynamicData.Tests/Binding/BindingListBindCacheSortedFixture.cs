#if SUPPORTS_BINDINGLIST

#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding
{
    public class BindingListBindCacheSortedFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection;

        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

        private readonly RandomPersonGenerator _generator = new();

        private readonly ISourceCache<Person, string> _source;

        public BindingListBindCacheSortedFixture()
        {
            _collection = new BindingList<Person>();
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect().Sort(_comparer, resetThreshold: 25).Bind(_collection).Subscribe();
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
        public async Task CollectionIsInSortOrder()
        {
            _source.AddOrUpdate(_generator.Take(100));
            var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
            await Assert.That(sorted).IsEquivalentTo(_collection.ToList());
        }

        [Test]
        public async Task LargeUpdateInvokesAReset()
        {
            //update once as initial load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            var invoked = false;
            ListChangedType? listChangedType = null;
            _collection.ListChanged += (sender, e) =>
                {
                    invoked = true;
                    listChangedType = e.ListChangedType;
                };
            _source.AddOrUpdate(_generator.Take(100));

            await Assert.That(invoked).IsTrue();
            await Assert.That(listChangedType).IsEqualTo(ListChangedType.Reset);
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

            await Assert.That(_collection[args.NewIndex]).IsEqualTo(people[10]);
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
        public async Task SmallChangeDoesNotInvokeReset()
        {
            //update once as initial load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            var invoked = false;
            var resetInvoked = false;
            _collection.ListChanged += (sender, e) =>
                {
                    invoked = true;
                    if (e.ListChangedType == ListChangedType.Reset)
                    {
                        resetInvoked = true;
                    }
                };
            _source.AddOrUpdate(_generator.Take(24));

            await Assert.That(invoked).IsTrue();
            await Assert.That(resetInvoked).IsFalse();
        }

        [Test]
        public async Task TreatMovesAsRemoveAdd()
        {
            var cache = new SourceCache<Person, string>(p => p.Name);

            var people = Enumerable.Range(0, 10).Select(age => new Person("Person" + age, age)).ToList();
            var importantGuy = people.First();
            cache.AddOrUpdate(people);

            ISortedChangeSet<Person, string>? latestSetWithoutMoves = null;
            ISortedChangeSet<Person, string>? latestSetWithMoves = null;

            var boundList1 = new ObservableCollectionExtended<Person>();
            var boundList2 = new ObservableCollectionExtended<Person>();

            using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).TreatMovesAsRemoveAdd().Bind(boundList1).Subscribe(set => latestSetWithoutMoves = set))

            using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Bind(boundList2).Subscribe(set => latestSetWithMoves = set))
            {
                if (latestSetWithoutMoves is null)
                {
                    throw new InvalidOperationException(nameof(latestSetWithoutMoves));
                }

                if (latestSetWithMoves is null)
                {
                    throw new InvalidOperationException(nameof(latestSetWithMoves));
                }

                importantGuy.Age += 200;
                await Assert.That(latestSetWithoutMoves).IsNotNull();
                await Assert.That(latestSetWithoutMoves.Removes).IsEqualTo(1);
                await Assert.That(latestSetWithoutMoves.Adds).IsEqualTo(1);
                await Assert.That(latestSetWithoutMoves.Moves).IsEqualTo(0);
                await Assert.That(latestSetWithoutMoves.Updates).IsEqualTo(0);

                await Assert.That(latestSetWithMoves.Moves).IsEqualTo(1);
                await Assert.That(latestSetWithMoves.Updates).IsEqualTo(0);
                await Assert.That(latestSetWithMoves.Removes).IsEqualTo(0);
                await Assert.That(latestSetWithMoves.Adds).IsEqualTo(0);
            }
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
