using System;
using System.Linq;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class FullJoinFixture : IDisposable
{
    private readonly SourceCache<Device, string> _left;

    private readonly ChangeSetAggregator<DeviceWithMetadata, string> _result;

    private readonly SourceCache<DeviceMetaData, string> _right;

    public FullJoinFixture()
    {
        _left = new SourceCache<Device, string>(device => device.Name);
        _right = new SourceCache<DeviceMetaData, string>(device => device.Name);

        _result = _left.Connect().FullJoin(_right.Connect(), meta => meta.Name, (key, device, meta) => new DeviceWithMetadata(key, device, meta)).AsAggregator();
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

        _result.Data.Count.Should().Be(3);
        _result.Data.Lookup("Device1").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device2").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device3").HasValue.Should().BeTrue();

        _result.Data.Items.All(dwm => dwm.MetaData == Optional<DeviceMetaData>.None).Should().BeTrue();
        _result.Data.Items.All(dwm => dwm.Device != Optional<Device>.None).Should().BeTrue();
    }

    [Fact]
    public void AddLetThenRight()
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
                innerCache.AddOrUpdate(new DeviceMetaData("Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device3"));
            });

        _result.Data.Count.Should().Be(3);

        _result.Data.Items.All(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().BeTrue();
    }

    [Fact]
    public void AddRightOnly()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData("Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device3"));
            });

        _result.Data.Count.Should().Be(3);
        _result.Data.Lookup("Device1").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device2").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device3").HasValue.Should().BeTrue();
        _result.Data.Items.All(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().BeTrue();
        _result.Data.Items.All(dwm => dwm.Device == Optional<Device>.None).Should().BeTrue();
    }

    [Fact]
    public void AddRightThenLeft()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData("Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _result.Data.Count.Should().Be(3);

        _result.Data.Items.All(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().BeTrue();
    }

    public void Dispose()
    {
        _left.Dispose();
        _right.Dispose();
        _result.Dispose();
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
                innerCache.AddOrUpdate(new DeviceMetaData("Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device3"));
            });
        _result.Data.Lookup("Device1").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device2").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device3").HasValue.Should().BeTrue();

        _right.Remove("Device3");

        _result.Data.Count.Should().Be(3);
        _result.Data.Items.Count(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().Be(2);

        _left.Remove("Device1");
        _result.Data.Lookup("Device1").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device2").HasValue.Should().BeTrue();
        _result.Data.Lookup("Device3").HasValue.Should().BeTrue();
    }

    [Fact]
    public void UpdateRight()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData("Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData("Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        _result.Data.Count.Should().Be(3);

        _result.Data.Items.All(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().BeTrue();
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

    public class DeviceMetaData(string name, bool isAutoConnect = false) : IEquatable<DeviceMetaData>
    {
        public bool IsAutoConnect { get; } = isAutoConnect;

        public string Name { get; } = name;

        public static bool operator ==(DeviceMetaData left, DeviceMetaData right) => Equals(left, right);

        public static bool operator !=(DeviceMetaData left, DeviceMetaData right) => !Equals(left, right);

        public bool Equals(DeviceMetaData? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name) && IsAutoConnect == other.IsAutoConnect;
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

            return Equals((DeviceMetaData)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name is not null ? Name.GetHashCode() : 0) * 397) ^ IsAutoConnect.GetHashCode();
            }
        }

        public override string ToString() => $"Metadata: {Name}. IsAutoConnect = {IsAutoConnect}";
    }

    public class DeviceWithMetadata(string key, Optional<Device> device, Optional<DeviceMetaData> metaData) : IEquatable<DeviceWithMetadata>
    {
        public Optional<Device> Device { get; set; } = device;

        public string Key { get; } = key;

        public Optional<DeviceMetaData> MetaData { get; } = metaData;

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

        public override int GetHashCode() => (Key is not null ? Key.GetHashCode() : 0);

        public override string ToString() => $"{Key}: {Device} ({MetaData})";
    }

    private class Address
    {
        public int Id { get; }

        public int PersonId { get; }
    }

    private class Person
    {
        public int Id { get; }
    }
}
