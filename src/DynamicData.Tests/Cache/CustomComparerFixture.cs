using DynamicData.Annotations;
using DynamicData.Cache.Internal;
using DynamicData.Tests.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class CustomComparerFixture
    {
        public const string ExistingKey = "PersonThatExists";
        public const string NonExistentKey = "PersonThatDoesNotExist";
        public const string ExistingButMismatchedKey = "PersonThatEXISTS";

        private SourceCache<Person, string> Construct(IEqualityComparer<string> keyEqualityComparer)
        {
            var people = new SourceCache<Person, string>(p => p.Name, keyEqualityComparer: keyEqualityComparer);
            people.AddOrUpdate(Enumerable.Range(1, 10)
                .Select(i => new Person("Person" + i, i)));
            people.AddOrUpdate(new Person(ExistingKey, 15));
            return people;
        }

        private ReaderWriter<Person, string> ConstructReaderWriter(IEqualityComparer<string> keyEqualityComparer)
        {
            var people = new ReaderWriter<Person, string>(p => p.Name, keyEqualityComparer: keyEqualityComparer);
            people.Write(
                updateAction: (updater) =>
                {
                    updater.AddOrUpdate(Enumerable.Range(1, 10)
                        .Select(i => new Person("Person" + i, i)));
                    updater.AddOrUpdate(new Person(ExistingKey, 15));
                },
                previewHandler: null,
                collectChanges: false);
            return people;
        }

        [Fact]
        public void DefaultComparer()
        {
            using (var people = Construct(keyEqualityComparer: null))
            {
                Assert.True(people.Lookup(ExistingKey).HasValue);
                Assert.False(people.Lookup(NonExistentKey).HasValue);
                Assert.False(people.Lookup(ExistingButMismatchedKey).HasValue);
            }
        }

        [Fact]
        public void CustomComparer()
        {
            using (var people = Construct(keyEqualityComparer: StringComparer.OrdinalIgnoreCase))
            {
                Assert.True(people.Lookup(ExistingKey).HasValue);
                Assert.False(people.Lookup(NonExistentKey).HasValue);
                Assert.True(people.Lookup(ExistingButMismatchedKey).HasValue);
            }
        }

        [Fact]
        public void ReaderWriterDefaultComparer()
        {
            var people = ConstructReaderWriter(keyEqualityComparer: null);
            Assert.True(people.Lookup(ExistingKey).HasValue);
            Assert.False(people.Lookup(NonExistentKey).HasValue);
            Assert.False(people.Lookup(ExistingButMismatchedKey).HasValue);
        }

        [Fact]
        public void ReaderWriterCustomComparer()
        {
            var people = ConstructReaderWriter(keyEqualityComparer: StringComparer.OrdinalIgnoreCase);
            Assert.True(people.Lookup(ExistingKey).HasValue);
            Assert.False(people.Lookup(NonExistentKey).HasValue);
            Assert.True(people.Lookup(ExistingButMismatchedKey).HasValue);
        }

        [Fact]
        public void ReaderWriterDuringPreview()
        {
            var people = ConstructReaderWriter(keyEqualityComparer: null);
            people.Write(
                updateAction: (updater) =>
                {
                    Assert.True(updater.Lookup(ExistingKey).HasValue);
                    Assert.False(updater.Lookup(NonExistentKey).HasValue);
                    Assert.False(updater.Lookup(ExistingButMismatchedKey).HasValue);
                    Assert.True(people.Lookup(ExistingKey).HasValue);
                    Assert.False(people.Lookup(NonExistentKey).HasValue);
                    Assert.False(people.Lookup(ExistingButMismatchedKey).HasValue);
                },
                previewHandler: (preview) =>
                {
                    Assert.True(people.Lookup(ExistingKey).HasValue);
                    Assert.False(people.Lookup(NonExistentKey).HasValue);
                    Assert.False(people.Lookup(ExistingButMismatchedKey).HasValue);
                },
                collectChanges: false);
        }

        [Fact]
        public void ReaderWriterCustomComparerDuringPreview()
        {
            var people = ConstructReaderWriter(keyEqualityComparer: StringComparer.OrdinalIgnoreCase);
            people.Write(
                updateAction: (updater) =>
                {
                    Assert.True(updater.Lookup(ExistingKey).HasValue);
                    Assert.False(updater.Lookup(NonExistentKey).HasValue);
                    Assert.True(updater.Lookup(ExistingButMismatchedKey).HasValue);
                    Assert.True(people.Lookup(ExistingKey).HasValue);
                    Assert.False(people.Lookup(NonExistentKey).HasValue);
                    Assert.True(people.Lookup(ExistingButMismatchedKey).HasValue);
                },
                previewHandler: (preview) =>
                {
                    Assert.True(people.Lookup(ExistingKey).HasValue);
                    Assert.False(people.Lookup(NonExistentKey).HasValue);
                    Assert.True(people.Lookup(ExistingButMismatchedKey).HasValue);
                },
                collectChanges: false);
        }
    }
}
