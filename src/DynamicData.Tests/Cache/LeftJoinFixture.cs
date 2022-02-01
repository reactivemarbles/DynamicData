using System;
using System.Linq;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache
{
    public class LeftJoinFixture : IDisposable
    {
        private readonly SourceCache<Device, string> _left;

        private readonly ChangeSetAggregator<DeviceWithMetadata, string> _result;

        private readonly SourceCache<DeviceMetaData, string> _right;

        public LeftJoinFixture()
        {
            _left = new SourceCache<Device, string>(device => device.Name);
            _right = new SourceCache<DeviceMetaData, string>(device => device.Name);

            _result = _left.Connect().LeftJoin(_right.Connect(), meta => meta.Name, (key, device, meta) => new DeviceWithMetadata(device, meta)).AsAggregator();
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

            _result.Data.Count.Should().Be(0);
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

            _right.Remove("Device3");

            _result.Data.Count.Should().Be(3);
            _result.Data.Items.Count(dwm => dwm.MetaData != Optional<DeviceMetaData>.None).Should().Be(2);

            _left.Remove("Device1");
            _result.Data.Lookup("Device1").HasValue.Should().BeFalse();
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

        public class Device : IEquatable<Device>
        {
            public Device(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public static bool operator ==(Device left, Device right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Device left, Device right)
            {
                return !Equals(left, right);
            }

            public bool Equals(Device? other)
            {
                if (ReferenceEquals(null, other))
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
                if (ReferenceEquals(null, obj))
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

            public override int GetHashCode()
            {
                return (Name is not null ? Name.GetHashCode() : 0);
            }

            public override string ToString()
            {
                return $"{Name}";
            }
        }

        public class DeviceMetaData : IEquatable<DeviceMetaData>
        {
            public DeviceMetaData(string name, bool isAutoConnect = false)
            {
                Name = name;
                IsAutoConnect = isAutoConnect;
            }

            public bool IsAutoConnect { get; }

            public string Name { get; }

            public static bool operator ==(DeviceMetaData left, DeviceMetaData right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(DeviceMetaData left, DeviceMetaData right)
            {
                return !Equals(left, right);
            }

            public bool Equals(DeviceMetaData? other)
            {
                if (ReferenceEquals(null, other))
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
                if (ReferenceEquals(null, obj))
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
                    return (Name.GetHashCode() * 397) ^ IsAutoConnect.GetHashCode();
                }
            }

            public override string ToString()
            {
                return $"Metadata: {Name}. IsAutoConnect = {IsAutoConnect}";
            }
        }

        public class DeviceWithMetadata : IEquatable<DeviceWithMetadata>
        {
            public DeviceWithMetadata(Device device, Optional<DeviceMetaData> metaData)
            {
                Device = device;
                MetaData = metaData;
            }

            public Device Device { get; }

            public Optional<DeviceMetaData> MetaData { get; }

            public static bool operator ==(DeviceWithMetadata left, DeviceWithMetadata right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(DeviceWithMetadata left, DeviceWithMetadata right)
            {
                return !Equals(left, right);
            }

            public bool Equals(DeviceWithMetadata? other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return Equals(Device, other.Device) && MetaData.Equals(other.MetaData);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj is DeviceWithMetadata value && Equals(value);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Device.GetHashCode() * 397) ^ MetaData.GetHashCode();
                }
            }

            public override string ToString()
            {
                return $"{Device} ({MetaData})";
            }
        }
    }
}