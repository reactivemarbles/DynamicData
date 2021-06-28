using System;
using System.Linq;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache
{
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

            0.Should().Be(_result.Data.Count);
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

            3.Should().Be(_result.Data.Count);

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

            3.Should().Be(_result.Data.Count);
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

            3.Should().Be(_result.Data.Count);

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

            2.Should().Be(_result.Data.Count);
            2.Should().Be(_result.Data.Items.Count(dwm => dwm.Device != Optional<Device>.None));

            _left.Remove("Device1");
            _result.Data.Lookup(1).HasValue.Should().BeTrue();
            1.Should().Be(_result.Data.Items.Count(dwm => dwm.Device == Optional<Device>.None));
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

            3.Should().Be(_result.Data.Count);

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

            3.Should().Be(_result.Data.Count);

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

            4.Should().Be(_result.Data.Count);

            _result.Data.Lookup(4).HasValue.Should().BeTrue();
            _result.Data.Items.Count(dwm => dwm.Device == Optional<Device>.None).Should().Be(1);
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
            public DeviceMetaData(int key, string name, bool isAutoConnect = false)
            {
                Key = key;
                Name = name;
                IsAutoConnect = isAutoConnect;
            }

            public bool IsAutoConnect { get; }
            public int Key { get; }
            public string Name { get; }

            public override string ToString()
            {
                return $"Key: {Key}. Metadata: {Name}. IsAutoConnect = {IsAutoConnect}";
            }

            public bool Equals(DeviceMetaData? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return IsAutoConnect == other.IsAutoConnect && Key == other.Key && Name == other.Name;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DeviceMetaData) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(IsAutoConnect, Key, Name);
            }
        }

        public class DeviceWithMetadata : IEquatable<DeviceWithMetadata>
        {
            public DeviceWithMetadata(int key, Optional<Device> device, DeviceMetaData metaData)
            {
                Key = key;
                Device = device;
                MetaData = metaData;
            }

            public Optional<Device> Device { get; }

            public int Key { get; }

            public DeviceMetaData MetaData { get; }

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

                return string.Equals(Key, other.Key);
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

                return Equals((DeviceWithMetadata)obj);
            }

            public override int GetHashCode()
            {
                return Key;
            }

            public override string ToString()
            {
                return $"{Key}: {Device} ({MetaData})";
            }
        }
    }
}