#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Cache;

public class LeftJoinFixture : IDisposable
{
    private readonly SourceCache<Device, string> _left;

    private readonly ChangeSetAggregator<DeviceWithMetadata, string> _result;

    private readonly SourceCache<DeviceMetaData, int> _right;

    public LeftJoinFixture()
    {
        _left = new SourceCache<Device, string>(device => device.Name);
        _right = new SourceCache<DeviceMetaData, int>(device => device.Key);

        _result = _left.Connect().LeftJoin(_right.Connect(), meta => meta.Name, (key, device, meta) => new DeviceWithMetadata(device, meta)).AsAggregator();
    }

    [Test]
    public async Task AddLeftOnly()
    {
        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Lookup("Device1").HasValue).IsTrue();
        await Assert.That(_result.Data.Lookup("Device2").HasValue).IsTrue();
        await Assert.That(_result.Data.Lookup("Device3").HasValue).IsTrue();

        await Assert.That(_result.Data.Items.All(dwm => dwm.MetaData == ReactiveUI.Primitives.Optional<DeviceMetaData>.None)).IsTrue();
    }

    [Test]
    public async Task AddLetThenRight()
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
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        await Assert.That(_result.Data.Count).IsEqualTo(3);

        await Assert.That(_result.Data.Items.All(dwm => dwm.MetaData != ReactiveUI.Primitives.Optional<DeviceMetaData>.None)).IsTrue();
    }

    [Test]
    public async Task AddRightOnly()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        await Assert.That(_result.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddRightThenLeft()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        await Assert.That(_result.Data.Count).IsEqualTo(3);

        await Assert.That(_result.Data.Items.All(dwm => dwm.MetaData != ReactiveUI.Primitives.Optional<DeviceMetaData>.None)).IsTrue();
    }

    public void Dispose()
    {
        _left.Dispose();
        _right.Dispose();
        _result.Dispose();
    }

    [Test]
    public async Task RefreshRightKey()
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
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
            });

        var refreshItem = _right.Lookup(2).Value;

        // Change pairing
        refreshItem.Name = "Device3";
        _right.Refresh(refreshItem);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).DoesNotContain(("Device2", 2));
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device3", 2));

        // Remove pairing
        refreshItem.Name = "Device4";
        _right.Refresh(refreshItem);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).DoesNotContain(pair => pair.Key == 2);

        // Restore pairing
        refreshItem.Name = "Device2";
        _right.Refresh(refreshItem);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device2", 2));

        // No change
        _right.Refresh(refreshItem);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device2", 2));
    }

    [Test]
    public async Task RemoveVarious()
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
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        _right.Remove(3);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Count(dwm => dwm.MetaData != ReactiveUI.Primitives.Optional<DeviceMetaData>.None)).IsEqualTo(2);

        _left.Remove("Device1");
        await Assert.That(_result.Data.Lookup("Device1").HasValue).IsFalse();
    }

    [Test]
    public async Task UpdateRight()
    {
        _right.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        _left.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Device("Device1"));
                innerCache.AddOrUpdate(new Device("Device2"));
                innerCache.AddOrUpdate(new Device("Device3"));
            });

        await Assert.That(_result.Data.Count).IsEqualTo(3);

        await Assert.That(_result.Data.Items.All(dwm => dwm.MetaData != ReactiveUI.Primitives.Optional<DeviceMetaData>.None)).IsTrue();
    }

    [Test]
    public async Task UpdateRightKey()
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
                innerCache.AddOrUpdate(new DeviceMetaData(1, "Device1"));
                innerCache.AddOrUpdate(new DeviceMetaData(2, "Device2"));
                innerCache.AddOrUpdate(new DeviceMetaData(3, "Device3"));
            });

        // Change pairing
        _right.AddOrUpdate(new DeviceMetaData(2, "Device3"));

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).DoesNotContain(("Device2", 2));
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device3", 2));

        // Remove pairing
        _right.AddOrUpdate(new DeviceMetaData(2, "Device4"));

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).DoesNotContain(pair => pair.Key == 2);

        // Restore pairing
        _right.AddOrUpdate(new DeviceMetaData(2, "Device2"));

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device2", 2));

        // No change
        _right.AddOrUpdate(new DeviceMetaData(2, "Device2"));

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.Select(pair => (pair.Device.Name, pair.MetaData.ValueOrDefault()?.Key))).Contains(("Device2", 2));
    }

    [Test]
    public async Task InitializationWaitsForBothSources()
    {
        // https://github.com/reactivemarbles/DynamicData/issues/943

        var left = new[] { 1, 2, 3 };
        var right = new[] { 4, 6, 2 };

        ObservableCacheEx
            .LeftJoin(
                left: left.AsObservableChangeSet(static left => 2 * left),
                right: right.AsObservableChangeSet(static right => right),
                rightKeySelector: static right => right,
                resultSelector: static (left, right) => (left, right: right.HasValue ? right.Value : default(int?)))
            .ValidateSynchronization()
            .ValidateChangeSets(static pair => 2 * pair.left)
            .RecordCacheItems(out var results);

        await Assert.That(results.Error).IsNull();

        await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("Initialization should only emit one changeset.");
        await Assert.That(results.RecordedChangeSets[0]).ContainsOnly(change => change.Reason == ChangeReason.Add).Because("Initialization should only emit Add changes.");

        await Assert.That(results.RecordedItemsByKey.Values).ContainsOnly(pair => (2 * pair.left) == pair.right).Because("Source items should have been joined correctly");
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
            return Equals((DeviceMetaData)obj);
        }

        public override int GetHashCode() => HashCode.Combine(IsAutoConnect, Key, Name);
    }

    public class DeviceWithMetadata(Device device, ReactiveUI.Primitives.Optional<DeviceMetaData> metaData) : IEquatable<DeviceWithMetadata>
    {
        public Device Device { get; } = device;

        public ReactiveUI.Primitives.Optional<DeviceMetaData> MetaData { get; } = metaData;

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

            return Equals(Device, other.Device) && MetaData.Equals(other.MetaData);
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

            return obj is DeviceWithMetadata value && Equals(value);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Device.GetHashCode() * 397) ^ MetaData.GetHashCode();
            }
        }

        public override string ToString() => $"{Device} ({MetaData})";
    }
}
