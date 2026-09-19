namespace DynamicData.Tests.Cache;

public class TransformTreeFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.StateSignal<Func<Node<EmployeeDto, int>, bool>> _filter;

    private readonly IObservableCache<Node<EmployeeDto, int>, int> _result;

    private readonly ISourceCache<EmployeeDto, int> _sourceCache;

    public TransformTreeFixture()
    {
        _sourceCache = new SourceCache<EmployeeDto, int>(e => e.Id);

        _filter = new ReactiveUI.Primitives.Signals.StateSignal<Func<Node<EmployeeDto, int>, bool>>(n => n.IsRoot);

        _result = _sourceCache.Connect().TransformToTree(e => e.BossId, _filter).AsObservableCache();
    }

    [Test]
    public async Task AddMissingChild()
    {
        var boss = new EmployeeDto(2) { BossId = 0, Name = "Boss" };
        var minion = new EmployeeDto(1) { BossId = 2, Name = "DogsBody" };
        _sourceCache.AddOrUpdate(boss);
        _sourceCache.AddOrUpdate(minion);

        await Assert.That(_result.Count).IsEqualTo(1);

        var firstNode = _result.Items[0];
        await Assert.That(firstNode.Item).IsEqualTo(boss);

        var childNode = firstNode.Children.Items[0];
        await Assert.That(childNode.Item).IsEqualTo(minion);
    }

    [Test]
    public async Task AddMissingParent()
    {
        var emp10 = new EmployeeDto(10) { BossId = 11, Name = "Employee10" };
        var emp11 = new EmployeeDto(11) { BossId = 0, Name = "Employee11" };
        var emp12 = new EmployeeDto(12) { BossId = 13, Name = "Employee12" };
        var emp13 = new EmployeeDto(13) { BossId = 11, Name = "Employee13" };

        _sourceCache.AddOrUpdate(emp10);
        _sourceCache.AddOrUpdate(emp11);
        _sourceCache.AddOrUpdate(emp12);
        _sourceCache.AddOrUpdate(emp13);

        await Assert.That(_result.Count).IsEqualTo(1);

        var emp11Node = _result.Lookup(11);
        await Assert.That(emp11Node.HasValue).IsTrue();
        await Assert.That(emp11Node.Value.Children.Count).IsEqualTo(2);

        var emp10Node = emp11Node.Value.Children.Lookup(10);
        await Assert.That(emp10Node.HasValue).IsTrue();
        await Assert.That(emp10Node.Value.Children.Count).IsEqualTo(0);

        var emp13Node = emp11Node.Value.Children.Lookup(13);
        await Assert.That(emp13Node.HasValue).IsTrue();
        await Assert.That(emp13Node.Value.Children.Count).IsEqualTo(1);

        var emp12Node = emp13Node.Value.Children.Lookup(12);
        await Assert.That(emp12Node.HasValue).IsTrue();
        await Assert.That(emp12Node.Value.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildTreeFromMixedData()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        await Assert.That(_result.Count).IsEqualTo(2);

        var firstNode = _result.Items[0];
        await Assert.That(firstNode.Children.Count).IsEqualTo(3);

        var secondNode = _result.Items.Skip(1).First();
        await Assert.That(secondNode.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ChangeParent()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        _sourceCache.AddOrUpdate(
            new EmployeeDto(4)
            {
                BossId = 1,
                Name = "Employee4"
            });

        //if this throws, then employee 4 is no a child of boss 1
        var emp4 = _result.Lookup(1).Value.Children.Lookup(4).Value;

        //check boss is = 1
        await Assert.That(emp4.Parent.Value.Item.Id).IsEqualTo(1);

        //lookup previous boss (emp 4 should no longet be a child)
        var emp3 = _result.Lookup(1).Value.Children.Lookup(3).Value;

        //emp 4 must be removed from previous boss's child collection
        await Assert.That(emp3.Children.Lookup(4).HasValue).IsFalse();
    }

    public void Dispose()
    {
        _sourceCache.Dispose();
        _result.Dispose();
        _filter.Dispose();
    }

    [Test]
    public async Task RemoveAChildNodeWillPushOrphansUpTheHierachy()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        _sourceCache.Remove(4);

        //we expect the children of node 4  to be pushed up become new roots
        await Assert.That(_result.Count).IsEqualTo(3);

        var thirdNode = _result.Items.Skip(2).First();
        await Assert.That(thirdNode.Key).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveARootNodeWillPushOrphansUpTheHierachy()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        _sourceCache.Remove(1);

        //we expect the original children nodes to be pushed up become new roots
        await Assert.That(_result.Count).IsEqualTo(4);
    }

    [Test]
    public async Task UpdateAParentNode()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        var changed = new EmployeeDto(1)
        {
            BossId = 0,
            Name = "Employee 1 (with name change)"
        };

        _sourceCache.AddOrUpdate(changed);
        await Assert.That(_result.Count).IsEqualTo(2);

        var firstNode = _result.Items[0];
        await Assert.That(firstNode.Children.Count).IsEqualTo(3);
        await Assert.That(firstNode.Item.Name).IsEqualTo(changed.Name);
    }

    [Test]
    public async Task UpdateChildNode()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        var changed = new EmployeeDto(2)
        {
            BossId = 1,
            Name = "Employee 2 (with name change)"
        };

        _sourceCache.AddOrUpdate(changed);
        await Assert.That(_result.Count).IsEqualTo(2);

        var changedNode = _result.Items[0].Children.Items[0];

        await Assert.That(changedNode.Parent.Value.Item.Id).IsEqualTo(1);
        await Assert.That(changedNode.Children.Count).IsEqualTo(1);
        await Assert.That(changedNode.Item.Name).IsEqualTo(changed.Name);
    }

    [Test]
    public async Task UseCustomFilter()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        await Assert.That(_result.Count).IsEqualTo(2);

        _filter.OnNext(node => true);
        await Assert.That(_result.Count).IsEqualTo(8);

        _filter.OnNext(node => node.Depth == 3);
        await Assert.That(_result.Count).IsEqualTo(1);

        _sourceCache.RemoveKey(5);
        await Assert.That(_result.Count).IsEqualTo(0);

        _filter.OnNext(node => node.IsRoot);
        await Assert.That(_result.Count).IsEqualTo(2);
    }

    private static IEnumerable<EmployeeDto> CreateEmployees()
    {
        yield return new EmployeeDto(1)
        {
            BossId = 0,
            Name = "Employee1"
        };

        yield return new EmployeeDto(2)
        {
            BossId = 1,
            Name = "Employee2"
        };

        yield return new EmployeeDto(3)
        {
            BossId = 1,
            Name = "Employee3"
        };

        yield return new EmployeeDto(4)
        {
            BossId = 3,
            Name = "Employee4"
        };

        yield return new EmployeeDto(5)
        {
            BossId = 4,
            Name = "Employee5"
        };

        yield return new EmployeeDto(6)
        {
            BossId = 2,
            Name = "Employee6"
        };

        yield return new EmployeeDto(7)
        {
            BossId = 0,
            Name = "Employee7"
        };

        yield return new EmployeeDto(8)
        {
            BossId = 1,
            Name = "Employee8"
        };
    }

    public class EmployeeDto(int id) : IEquatable<EmployeeDto>
    {
        public int BossId { get; set; }

        public int Id { get; set; } = id;

        public string? Name { get; set; }

        public static bool operator ==(EmployeeDto left, EmployeeDto right) => Equals(left, right);

        public static bool operator !=(EmployeeDto left, EmployeeDto right) => !Equals(left, right);

        public bool Equals(EmployeeDto? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((EmployeeDto)obj);
        }

        public override int GetHashCode() => Id;

        public override string ToString() => $"Name: {Name}, Id: {Id}, BossId: {BossId}";
    }
}
