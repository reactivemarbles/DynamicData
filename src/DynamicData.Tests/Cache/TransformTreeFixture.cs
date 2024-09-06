using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformTreeFixture : IDisposable
{
    private readonly BehaviorSubject<Func<Node<EmployeeDto, int>, bool>> _filter;

    private readonly IObservableCache<Node<EmployeeDto, int>, int> _result;

    private readonly ISourceCache<EmployeeDto, int> _sourceCache;

    public TransformTreeFixture()
    {
        _sourceCache = new SourceCache<EmployeeDto, int>(e => e.Id);

        _filter = new BehaviorSubject<Func<Node<EmployeeDto, int>, bool>>(n => n.IsRoot);

        _result = _sourceCache.Connect().TransformToTree(e => e.BossId, _filter).AsObservableCache();
    }

    [Fact]
    public void AddMissingChild()
    {
        var boss = new EmployeeDto(2) { BossId = 0, Name = "Boss" };
        var minion = new EmployeeDto(1) { BossId = 2, Name = "DogsBody" };
        _sourceCache.AddOrUpdate(boss);
        _sourceCache.AddOrUpdate(minion);

        _result.Count.Should().Be(1);

        var firstNode = _result.Items[0];
        firstNode.Item.Should().Be(boss);

        var childNode = firstNode.Children.Items[0];
        childNode.Item.Should().Be(minion);
    }

    [Fact]
    public void AddMissingParent()
    {
        var emp10 = new EmployeeDto(10) { BossId = 11, Name = "Employee10" };
        var emp11 = new EmployeeDto(11) { BossId = 0, Name = "Employee11" };
        var emp12 = new EmployeeDto(12) { BossId = 13, Name = "Employee12" };
        var emp13 = new EmployeeDto(13) { BossId = 11, Name = "Employee13" };

        _sourceCache.AddOrUpdate(emp10);
        _sourceCache.AddOrUpdate(emp11);
        _sourceCache.AddOrUpdate(emp12);
        _sourceCache.AddOrUpdate(emp13);

        _result.Count.Should().Be(1);

        var emp11Node = _result.Lookup(11);
        emp11Node.HasValue.Should().BeTrue();
        emp11Node.Value.Children.Count.Should().Be(2);

        var emp10Node = emp11Node.Value.Children.Lookup(10);
        emp10Node.HasValue.Should().BeTrue();
        emp10Node.Value.Children.Count.Should().Be(0);

        var emp13Node = emp11Node.Value.Children.Lookup(13);
        emp13Node.HasValue.Should().BeTrue();
        emp13Node.Value.Children.Count.Should().Be(1);

        var emp12Node = emp13Node.Value.Children.Lookup(12);
        emp12Node.HasValue.Should().BeTrue();
        emp12Node.Value.Children.Count.Should().Be(0);
    }

    [Fact]
    public void BuildTreeFromMixedData()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        _result.Count.Should().Be(2);

        var firstNode = _result.Items[0];
        firstNode.Children.Count.Should().Be(3);

        var secondNode = _result.Items.Skip(1).First();
        secondNode.Children.Count.Should().Be(0);
    }

    [Fact]
    public void ChangeParent()
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
        emp4.Parent.Value.Item.Id.Should().Be(1);

        //lookup previous boss (emp 4 should no longet be a child)
        var emp3 = _result.Lookup(1).Value.Children.Lookup(3).Value;

        //emp 4 must be removed from previous boss's child collection
        emp3.Children.Lookup(4).HasValue.Should().BeFalse();
    }

    public void Dispose()
    {
        _sourceCache.Dispose();
        _result.Dispose();
        _filter.Dispose();
    }

    [Fact]
    public void RemoveAChildNodeWillPushOrphansUpTheHierachy()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        _sourceCache.Remove(4);

        //we expect the children of node 4  to be pushed up become new roots
        _result.Count.Should().Be(3);

        var thirdNode = _result.Items.Skip(2).First();
        thirdNode.Key.Should().Be(5);
    }

    [Fact]
    public void RemoveARootNodeWillPushOrphansUpTheHierachy()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());
        _sourceCache.Remove(1);

        //we expect the original children nodes to be pushed up become new roots
        _result.Count.Should().Be(4);
    }

    [Fact]
    public void UpdateAParentNode()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        var changed = new EmployeeDto(1)
        {
            BossId = 0,
            Name = "Employee 1 (with name change)"
        };

        _sourceCache.AddOrUpdate(changed);
        _result.Count.Should().Be(2);

        var firstNode = _result.Items[0];
        firstNode.Children.Count.Should().Be(3);
        firstNode.Item.Name.Should().Be(changed.Name);
    }

    [Fact]
    public void UpdateChildNode()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        var changed = new EmployeeDto(2)
        {
            BossId = 1,
            Name = "Employee 2 (with name change)"
        };

        _sourceCache.AddOrUpdate(changed);
        _result.Count.Should().Be(2);

        var changedNode = _result.Items[0].Children.Items[0];

        changedNode.Parent.Value.Item.Id.Should().Be(1);
        changedNode.Children.Count.Should().Be(1);
        changed.Name.Should().Be(changed.Name);
    }

    [Fact]
    public void UseCustomFilter()
    {
        _sourceCache.AddOrUpdate(TransformTreeFixture.CreateEmployees());

        _result.Count.Should().Be(2);

        _filter.OnNext(node => true);
        _result.Count.Should().Be(8);

        _filter.OnNext(node => node.Depth == 3);
        _result.Count.Should().Be(1);

        _sourceCache.RemoveKey(5);
        _result.Count.Should().Be(0);

        _filter.OnNext(node => node.IsRoot);
        _result.Count.Should().Be(2);
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
