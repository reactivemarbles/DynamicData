using System;
using System.Linq;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class RightJoinFixture : IDisposable
{
    private readonly SourceCache<Device, string> _left;

    private readonly ChangeSetAggregator<DeviceWithMetadata, int> _result;

    private readonly SourceCache<DeviceMetaData, int> _right;

    public RightJoinFixture()
    {
        _left = new SourceCache<Device, string>(device => device.Name);
        _right = new SourceCache<DeviceMetaData, int>(device => device.Key);

        _result = _left.Connect().RightJoin(_right.Connect(), meta => meta.Name, (key, device, meta) => new DeviceWithMetadata(key, device, meta)).AsAggregator();
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

        _result.Data.Items.All(dwm => dwm.Device != Optional<Device>.None).Should().BeTrue();
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

        _result.Data.Count.Should().Be(3);
        _result.Data.Lookup(1).HasValue.Should().BeTrue();
        _result.Data.Lookup(2).HasValue.Should().BeTrue();
        _result.Data.Lookup(3).HasValue.Should().BeTrue();

        _result.Data.Items.All(dwm => dwm.Device == Optional<Device>.None).Should().BeTrue();
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

        _result.Data.Items.All(dwm => dwm.Device != Optional<Device>.None).Should().BeTrue();
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
                innerCache.AddOrUpdate(new DeviceMetaData(1,"Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2,"Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3,"Device3"));
            });

        _right.Remove(3);

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.Count(dwm => dwm.Device != Optional<Device>.None).Should().Be(2);

        _left.Remove("Device1");
        _result.Data.Lookup(1).HasValue.Should().BeTrue();
        _result.Data.Items.Count(dwm => dwm.Device == Optional<Device>.None).Should().Be(1);
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

        _result.Data.Items.All(dwm => dwm.Device != Optional<Device>.None).Should().BeTrue();
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

        _result.Data.Items.All(dwm => dwm.Device != Optional<Device>.None).Should().BeTrue();
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

        _result.Data.Count.Should().Be(4);

        _result.Data.Lookup(4).HasValue.Should().BeTrue();
        _result.Data.Items.Count(dwm => dwm.Device == Optional<Device>.None).Should().Be(1);
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
        public string Name { get; } = name;

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

    public class DeviceWithMetadata(int key, Optional<Device> device, DeviceMetaData metaData) : IEquatable<DeviceWithMetadata>
    {
        public Optional<Device> Device { get; } = device;

        public int Key { get; } = key;

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

        public override int GetHashCode() => Key;

        public override string ToString() => $"{Key}: {Device} ({MetaData})";
    }
}
