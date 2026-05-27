// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reflection;

using Bogus;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// A <see cref="Randomizer"/> whose underlying <see cref="Random"/> source is safe for
/// concurrent access. Multi-threaded stress tests share a single seeded randomizer across
/// many producer threads (directly and via <c>Faker&lt;T&gt;.WithSeed</c>); the default
/// <see cref="Randomizer"/> skips its internal locker when a localSeed is supplied, so a
/// shared instance races <see cref="Random"/> internal state and produces non-deterministic
/// failures. Replacing the protected <c>localSeed</c> field with a <see cref="LockedRandom"/>
/// serializes every generator call without changing the deterministic seed.
/// </summary>
internal sealed class SynchronizedRandomizer : Randomizer
{
    private static readonly FieldInfo LocalSeedField =
        typeof(Randomizer).GetField("localSeed", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Bogus.Randomizer.localSeed field not found; library shape changed.");

    public SynchronizedRandomizer(int seed)
        : base(seed) =>
        LocalSeedField.SetValue(this, new LockedRandom(seed));

    private sealed class LockedRandom(int seed) : Random(seed)
    {
        private readonly object _gate = new();

        public override int Next() { lock (_gate) { return base.Next(); } }

        public override int Next(int maxValue) { lock (_gate) { return base.Next(maxValue); } }

        public override int Next(int minValue, int maxValue) { lock (_gate) { return base.Next(minValue, maxValue); } }

        public override double NextDouble() { lock (_gate) { return base.NextDouble(); } }

        public override void NextBytes(byte[] buffer) { lock (_gate) { base.NextBytes(buffer); } }

        public override void NextBytes(Span<byte> buffer) { lock (_gate) { base.NextBytes(buffer); } }

        public override long NextInt64() { lock (_gate) { return base.NextInt64(); } }

        public override long NextInt64(long maxValue) { lock (_gate) { return base.NextInt64(maxValue); } }

        public override long NextInt64(long minValue, long maxValue) { lock (_gate) { return base.NextInt64(minValue, maxValue); } }

        public override float NextSingle() { lock (_gate) { return base.NextSingle(); } }

        protected override double Sample() { lock (_gate) { return base.Sample(); } }
    }
}
