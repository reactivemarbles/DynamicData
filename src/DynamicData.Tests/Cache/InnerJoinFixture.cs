using System;

using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class InnerJoinFixture : IDisposable
{
    private readonly SourceCache<Device, string> _left;

    private readonly ChangeSetAggregator<DeviceWithMetadata, (string leftKey,int rightKey)> _result;

    private readonly SourceCache<DeviceMetaData, int> _right;

    public InnerJoinFixture()
    {
        _left = new SourceCache<Device, string>(device => device.Name);
        _right = new SourceCache<DeviceMetaData, int>(device => device.Key);

        _result = _left.Connect().InnerJoin(_right.Connect(), meta => meta.Name, (key, device, meta) => new DeviceWithMetadata(key, device, meta)).AsAggregator();
    }

    [Fact]
    public void AddLeftOnly()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _result.Data.Count.Should().Be(0);
    }

    [Fact]
    public void AddLeftThenRight()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _result.Data.Count.Should().Be(3);
    }

    [Fact]
    public void AddRightOnly()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _result.Data.Count.Should().Be(0);
    }

    [Fact]
    public void AddRightThenLeft()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _result.Data.Count.Should().Be(3);
    }

    public void Dispose()
    {
        _left?.Dispose();
        _right?.Dispose();
        _result?.Dispose();
    }

    [Fact]
    public void RefreshRightKey()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        var refreshItem = _right.Lookup(2).Value;


        // Change pairing
        refreshItem.Name = "Device3";
        _right.Refresh(refreshItem);

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device3", 2));


        // Remove pairing
        refreshItem.Name = "Device4";
        _right.Refresh(refreshItem);

        _result.Data.Count.Should().Be(2);
        _result.Data.Keys.Should().NotContain(pair => pair.rightKey == 2);


        // Restore pairing
        refreshItem.Name = "Device2";
        _right.Refresh(refreshItem);

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device2", 2));


        // No change
        _right.Refresh(refreshItem);

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device2", 2));
    }

    [Fact]
    public void RemoveVarious()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _result.Data.Lookup(("Device1",1)).HasValue.Should().BeTrue();
        _result.Data.Lookup(("Device2",2)).HasValue.Should().BeTrue();
        _result.Data.Lookup(("Device3",3)).HasValue.Should().BeTrue();

        _right.Remove(3);

        _result.Data.Count.Should().Be(2);

        _left.Remove("Device1");
        _result.Data.Count.Should().Be(1);
        _result.Data.Lookup(("Device1",1)).HasValue.Should().BeFalse();
        _result.Data.Lookup(("Device2",2)).HasValue.Should().BeTrue();
        _result.Data.Lookup(("Device3",3)).HasValue.Should().BeFalse();
    }

    [Fact]
    public void UpdateRight()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _result.Data.Count.Should().Be(3);
    }

    [Fact]
    public void UpdateRightKey()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        
        // Change pairing
        _right.AddOrUpdate(new DeviceMetaData(2,"Device3"));

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device3", 2));


        // Remove pairing
        _right.AddOrUpdate(new DeviceMetaData(2,"Device4"));

        _result.Data.Count.Should().Be(2);
        _result.Data.Keys.Should().NotContain(pair => pair.rightKey == 2);


        // Restore pairing
        _right.AddOrUpdate(new DeviceMetaData(2,"Device2"));

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device2", 2));


        // No change
        _right.AddOrUpdate(new DeviceMetaData(2,"Device2"));

        _result.Data.Count.Should().Be(3);
        _result.Data.Keys.Should().Contain(("Device2", 2));
    }

    [Fact]
    public void MultipleRight()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device3")); // deliberate!
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _result.Data.Count.Should().Be(3);
    }

    [Fact]
    public void MoreRight()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2")); 
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
                innerCache.AddOrUpdate(new DeviceMetaData(4,"Device4"));
            });

        _result.Data.Count.Should().Be(3);

        _result.Data.Lookup(("Device4",4)).HasValue.Should().BeFalse();

    }

    [Fact]
    public void InitializationWaitsForBothSources()
    {
        var left = new[] { 1, 2, 3 };
        var right = new[] { 4, 6, 2 };

        ObservableCacheEx
            .InnerJoin(
                left:               left.AsObservableChangeSet(static left => 2 * left),
                right:              right.AsObservableChangeSet(static right => right),
                rightKeySelector:   static right => right,
                resultSelector:     static (left, right) => (left, right))
            .ValidateSynchronization()
            .ValidateChangeSets(static pair => (2 * pair.left, pair.right))
            .RecordCacheItems(out var results);

        results.Error.Should().BeNull();

        results.RecordedChangeSets.Count.Should().Be(1, "Initialization should only emit one changeset.");
        results.RecordedChangeSets[0].Should().OnlyContain(change => change.Reason == ChangeReason.Add, "Initialization should only emit Add changes.");

        results.RecordedItemsByKey.Values.Should().OnlyContain(pair => (2 * pair.left) == pair.right, "Source items should have been joined correctly");
    }

    public class Device(string name) : IEquatable<Device>
    {
        public string Name { get; } = name;

        public static bool operator ==(Device left, Device right) => Equals(left, right);

        public static bool operator !=(Device left, Device right) => !Equals(left, right);

        public bool Equals(Device? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name);
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

            return Equals((Device)obj);
        }

        public override int GetHashCode() => (Name is not null ? Name.GetHashCode() : 0);

        public override string ToString() => $"{Name}";
    }

    public class DeviceMetaData(int key, string name, bool isAutoConnect = false) : IEquatable<DeviceMetaData>
    {
        public bool IsAutoConnect { get; } = isAutoConnect;
        public int Key { get; } = key;
        public string Name { get; set; } = name;

        public override string ToString() => $"Key: {Key}. Metadata: {Name}. IsAutoConnect = {IsAutoConnect}";

        public bool Equals(DeviceMetaData? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsAutoConnect == other.IsAutoConnect && Key == other.Key && Name == other.Name;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DeviceMetaData) obj);
        }

        public override int GetHashCode() => HashCode.Combine(IsAutoConnect, Key, Name);
    }

    public class DeviceWithMetadata((string leftKey, int rightKey) key, Device device, DeviceMetaData metaData) : IEquatable<DeviceWithMetadata>
    {
        public Device Device { get; set; } = device;

        public (string leftKey, int rightKey) Key { get; } = key;

        public DeviceMetaData MetaData { get; } = metaData;

        public static bool operator ==(DeviceWithMetadata left, DeviceWithMetadata right) => Equals(left, right);

        public static bool operator !=(DeviceWithMetadata left, DeviceWithMetadata right) => !Equals(left, right);

        public bool Equals(DeviceWithMetadata? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Key, other.Key);
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

            return Equals((DeviceWithMetadata)obj);
        }

        public override int GetHashCode() => (Key is { } key ? key.GetHashCode() : 0);

        public override string ToString() => $"{Key}: {Device} ({MetaData})";
    }
}
