using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class TransformManyFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithRelations> _results;

    private readonly ISourceList<PersonWithRelations> _source;

    public TransformManyFixture()
    {
        _source = new SourceList<PersonWithRelations>();

        _results = _source.Connect().TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations)).AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

        _source.Add(mother);

        await Assert.That(_results.Data.Count).IsEqualTo(4);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { child1, child2, child3, frientofchild1 });
    }

    [Test]
    public async Task AddRange()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });

        _source.Add(mother);

        var child4 = new PersonWithRelations("Child4", 1);
        var child5 = new PersonWithRelations("Child5", 2);
        var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });

        var child6 = new PersonWithRelations("Child6", 1);
        var child7 = new PersonWithRelations("Child7", 2);
        var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

        _source.AddRange(new[] { anotherRelative1, anotherRelative2 });
        await Assert.That(_results.Data.Count).IsEqualTo(8);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { child1, child2, child3, frientofchild1, child4, child5, child6, child7 });
    }

    [Test]
    public async Task Clear()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        var child4 = new PersonWithRelations("Child4", 1);
        var child5 = new PersonWithRelations("Child5", 2);
        var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
        var child6 = new PersonWithRelations("Child6", 1);
        var child7 = new PersonWithRelations("Child7", 2);
        var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

        _source.AddRange(new[] { mother, anotherRelative1, anotherRelative2 });

        _source.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task Move()
    {
        //Move should have no effect

        var child4 = new PersonWithRelations("Child4", 1);
        var child5 = new PersonWithRelations("Child5", 2);
        var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
        var child6 = new PersonWithRelations("Child6", 1);
        var child7 = new PersonWithRelations("Child7", 2);
        var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

        _source.AddRange(new[] { anotherRelative1, anotherRelative2 });

        await Assert.That(_results.Messages.Count).IsEqualTo(1);
        _source.Move(1, 0);
        await Assert.That(_results.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Remove()
    {
        var tourProviders = new SourceList<TourProvider>();

        var allTours = tourProviders.Connect().TransformMany(tourProvider => tourProvider.Tours).AsObservableList();

        var tour1_1 = new Tour("Tour 1.1");
        var tour2_1 = new Tour("Tour 2.1");
        var tour2_2 = new Tour("Tour 2.2");
        var tour3_1 = new Tour("Tour 3.1");

        var tp1 = new TourProvider("Tour provider 1", new[] { tour1_1 });
        var tp2 = new TourProvider("Tour provider 2", new[] { tour2_1, tour2_2 });
        var tp3 = new TourProvider("Tour provider 3", null);

        tourProviders.AddRange(new[] { tp1, tp2, tp3 });

        await Assert.That(allTours.Items).IsEquivalentTo(new[] { tour1_1, tour2_1, tour2_2 });

        tp3.Tours.Add(tour3_1);
        await Assert.That(allTours.Items).IsEquivalentTo(new[] { tour1_1, tour2_1, tour2_2, tour3_1 });

        tp2.Tours.Remove(tour2_1);
        await Assert.That(allTours.Items).IsEquivalentTo(new[] { tour1_1, tour2_2, tour3_1 });

        tp2.Tours.Add(tour2_1);
        await Assert.That(allTours.Items).IsEquivalentTo(new[] { tour1_1, tour2_1, tour2_2, tour3_1 });
    }

    [Test]
    public async Task RemoveParent()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });

        _source.Add(mother);
        _source.Remove(mother);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveRange()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        var child4 = new PersonWithRelations("Child4", 1);
        var child5 = new PersonWithRelations("Child5", 2);
        var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
        var child6 = new PersonWithRelations("Child6", 1);
        var child7 = new PersonWithRelations("Child7", 2);
        var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

        _source.AddRange(new[] { mother, anotherRelative1, anotherRelative2 });

        _source.RemoveRange(0, 2);
        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { child6, child7 });
    }

    [Test]
    public async Task Replace()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });

        _source.Add(mother);

        var child4 = new PersonWithRelations("Child4", 2);
        var updatedMother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child4 });

        _source.Replace(mother, updatedMother);

        await Assert.That(_results.Data.Count).IsEqualTo(4);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { child1, child2, frientofchild1, child4 });
    }

    public class Tour(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => $"{nameof(Name)}: {Name}";
    }

    public class TourProvider
    {
        public TourProvider(string name, IEnumerable<Tour>? tours)
        {
            Name = name;

            if (tours is not null)
            {
                Tours.AddRange(tours);
            }
        }

        public string Name { get; }

        public ObservableCollection<Tour> Tours { get; } = [];
    }
}
