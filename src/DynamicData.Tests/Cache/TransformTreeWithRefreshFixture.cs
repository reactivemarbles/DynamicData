using System;
using System.Collections.Generic;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformTreeWithRefreshFixture : IDisposable
{
    private readonly IObservableCache<Node<EmployeeDto, int>, int> _result;

    private readonly ISourceCache<EmployeeDto, int> _sourceCache;

    public TransformTreeWithRefreshFixture()
    {
        _sourceCache = new SourceCache<EmployeeDto, int>(e => e.Id);
        _result = _sourceCache.Connect().AutoRefresh().TransformToTree(e => e.BossId).AsObservableCache();
        _sourceCache.AddOrUpdate(CreateEmployees());
    }

    public void Dispose()
    {
        _sourceCache.Dispose();
        _result.Dispose();
    }

    [Fact]
    public void DoNotUpdateTreeWhenParentIdNotChanged()
    {
        _sourceCache.Lookup(1).Value.Name = "Employee11";
        _sourceCache.Lookup(2).Value.Name = "Employee22";

        var node1 = _result.Lookup(1);
        node1.HasValue.Should().BeTrue();
        node1.Value.Parent.HasValue.Should().BeFalse();
        var node2 = node1.Value.Children.Lookup(2);
        node2.HasValue.Should().BeTrue();
        node2.Value.Parent.HasValue.Should().BeTrue();
        node2.Value.Parent.Value.Key.Should().Be(1);
    }

    [Fact]
    public void UpdateTreeWhenParentIdOfNonRootItemChangedToExistingId()
    {
        _sourceCache.Lookup(2).Value.BossId = 3;

        // node 2 added to node 3 children cache
        var node2 = _result.Lookup(1).Value.Children.Lookup(3).Value.Children.Lookup(2);
        node2.HasValue.Should().BeTrue();
        node2.Value.IsRoot.Should().BeFalse();

        // node 2 removed from node 1 children cache
        _result.Lookup(1).Value.Children.Lookup(2).HasValue.Should().BeFalse();
    }

    [Fact]
    public void UpdateTreeWhenParentIdOfNonRootItemChangedToNonExistingId()
    {
        _sourceCache.Lookup(2).Value.BossId = 25;

        // node 2 added to root
        var node2 = _result.Lookup(2);
        node2.HasValue.Should().BeTrue();
        node2.Value.IsRoot.Should().BeTrue();

        // node 2 removed from node 1 children cache
        _result.Lookup(1).Value.Children.Lookup(2).HasValue.Should().BeFalse();
    }

    [Fact]
    public void UpdateTreeWhenParentIdOfRootItemChangedToExistingId()
    {
        _sourceCache.Lookup(1).Value.BossId = 7;

        // node 1 added to node 7 children cache
        var node1 = _result.Lookup(7).Value.Children.Lookup(1);
        node1.HasValue.Should().BeTrue();
        node1.Value.IsRoot.Should().BeFalse();

        // node 1 removed from root
        _result.Lookup(1).HasValue.Should().BeFalse();
    }

    [Fact]
    public void UpdateTreeWhenParentIdOfRootItemChangedToNonExistingId()
    {
        _sourceCache.Lookup(1).Value.BossId = 25;

        // node 1 added to node 7 children cache
        var node1 = _result.Lookup(1);
        node1.HasValue.Should().BeTrue();
        node1.Value.IsRoot.Should().BeTrue();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private IEnumerable<EmployeeDto> CreateEmployees()
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

    private class EmployeeDto(int id) : AbstractNotifyPropertyChanged, IEquatable<EmployeeDto>
    {
        private int _bossId;

        private string? _name;

        public int BossId
        {
            get => _bossId;
            set => SetAndRaise(ref _bossId, value);
        }

        public int Id { get; } = id;

        public string? Name
        {
            get => _name;
            set => SetAndRaise(ref _name, value);
        }

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
