using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class GroupControllerForFilteredItemsFixture : IDisposable
{
    private readonly IObservableCache<IGroup<Person, string, AgeBracket>, AgeBracket> _grouped;

    private readonly Func<Person, AgeBracket> _grouper = p =>
    {
        if (p.Age <= 19)
        {
            return AgeBracket.Under20;
        }

        return p.Age <= 60 ? AgeBracket.Adult : AgeBracket.Pensioner;
    };

    private readonly ReactiveUI.Primitives.Signals.Signal<Unit> _refreshSubject = new ReactiveUI.Primitives.Signals.Signal<Unit>();

    private readonly ISourceCache<Person, string> _source;

    public GroupControllerForFilteredItemsFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);

        _grouped = _source.Connect(p => _grouper(p) != AgeBracket.Pensioner).Group(_grouper, _refreshSubject).AsObservableCache();
    }

    private enum AgeBracket
    {
        Under20,

        Adult,

        Pensioner
    }

    public void Dispose()
    {
        _source.Dispose();
        _grouped.Dispose();
        _refreshSubject.Dispose();
    }

    [Test]
    public async Task RegroupRecaluatesGroupings()
    {
        var p1 = new Person("P1", 10);
        var p2 = new Person("P2", 15);
        var p3 = new Person("P3", 30);
        var p4 = new Person("P4", 70);
        var people = new[] { p1, p2, p3, p4 };

        _source.AddOrUpdate(people);

        await Assert.That(IsContainedIn("P1", AgeBracket.Under20)).IsTrue();
        await Assert.That(IsContainedIn("P2", AgeBracket.Under20)).IsTrue();
        await Assert.That(IsContainedIn("P3", AgeBracket.Adult)).IsTrue();

        p1.Age = 60;
        p2.Age = 80;
        p3.Age = 15;
        p4.Age = 30;

        _refreshSubject.OnNext(Unit.Default);

        await Assert.That(IsContainedIn("P1", AgeBracket.Adult)).IsTrue();
        await Assert.That(IsContainedIn("P3", AgeBracket.Under20)).IsTrue();

        await Assert.That(IsContainedOnlyInOneGroup("P1")).IsTrue();
        await Assert.That(IsContainedOnlyInOneGroup("P2")).IsTrue();
    }

    [Test]
    public async Task RegroupRecaluatesGroupings2()
    {
        var p1 = new Person("P1", 10);
        var p2 = new Person("P2", 15);
        var p3 = new Person("P3", 30);
        var p4 = new Person("P4", 70);
        var people = new[] { p1, p2, p3, p4 };

        _source.AddOrUpdate(people);

        await Assert.That(IsContainedIn("P1", AgeBracket.Under20)).IsTrue();
        await Assert.That(IsContainedIn("P2", AgeBracket.Under20)).IsTrue();
        await Assert.That(IsContainedIn("P3", AgeBracket.Adult)).IsTrue();
        await Assert.That(IsContainedIn("P4", AgeBracket.Pensioner)).IsFalse();

        p1.Age = 60;
        p2.Age = 80;
        p3.Age = 15;
        p4.Age = 30;

        // _controller.RefreshGroup();

        _source.Refresh(new[] { p1, p2, p3, p4 });

        await Assert.That(IsContainedIn("P1", AgeBracket.Adult)).IsTrue();
        await Assert.That(IsContainedIn("P2", AgeBracket.Pensioner)).IsFalse();
        await Assert.That(IsContainedIn("P3", AgeBracket.Under20)).IsTrue();
        await Assert.That(IsContainedIn("P4", AgeBracket.Adult)).IsTrue();

        await Assert.That(IsContainedOnlyInOneGroup("P1")).IsTrue();
        await Assert.That(IsNotContainedAnyWhere("P2")).IsTrue();
        await Assert.That(IsContainedOnlyInOneGroup("P3")).IsTrue();
        await Assert.That(IsContainedOnlyInOneGroup("P4")).IsTrue();
    }

    private bool IsContainedIn(string name, AgeBracket bracket)
    {
        var group = _grouped.Lookup(bracket);
        if (!group.HasValue)
        {
            return false;
        }

        return group.Value.Cache.Lookup(name).HasValue;
    }

    private bool IsContainedOnlyInOneGroup(string name)
    {
        var person = _grouped.Items.SelectMany(g => g.Cache.Items.Where(s => s.Name == name)).ToList();

        return person.Count == 1;
    }

    private bool IsNotContainedAnyWhere(string name)
    {
        var person = _grouped.Items.SelectMany(g => g.Cache.Items.Where(s => s.Name == name)).ToList();

        return person.Count == 0;
    }
}
