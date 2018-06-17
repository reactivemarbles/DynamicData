using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class ChangesReducerFixture
    {
        private static Person _testEntity = new Person("test", "test", 32);
        private static int _testIndex = 0;
        private static Change<Person, string>[] _changes = new[]
        {
            new Change<Person, string>(ChangeReason.Add, _testEntity.Key, _testEntity, _testIndex),
            new Change<Person, string>(ChangeReason.Remove, _testEntity.Key, _testEntity, _testIndex),
            new Change<Person, string>(ChangeReason.Moved, _testEntity.Key, _testEntity, _testEntity, _testIndex,
                _testIndex + 1),
            new Change<Person, string>(ChangeReason.Update, _testEntity.Key, _testEntity, _testEntity, _testIndex),
            new Change<Person, string>(ChangeReason.Refresh, _testEntity.Key, _testEntity, _testIndex)
        };


        [Theory]
        [MemberData(nameof(ConstrainFirstValue), new object[] {ChangeReason.Refresh, new ChangeReason[] {}})]
        public void RefreshIsBeingOverridenByAnything(Change<Person,string> refresh, Change<Person, string> other)
        {
            var result = ChangesReducer.Reduce(refresh, other);
            result.Value.Should().Be(other);
        }

        [Theory]
        [MemberData(nameof(ConstrainFirstValue), new object[] {ChangeReason.Remove, new[] { ChangeReason.Add}})]
        public void RemoveOverridesAnythingButAdd(Change<Person, string> remove, Change<Person, string> other)
        {
            var result = ChangesReducer.Reduce(other, remove);
            result.Value.Should().Be(remove);
        }


        [Theory]
        [MemberData(nameof(GetChanges))]
        public void NoneGetsOverridenByAnything(Change<Person, string> c)
        {
            var result = ChangesReducer.Reduce(Optional<Change<Person, string>>.None, c);
            result.Value.Should().Be(c);
        }

        [Fact]
        public void AddAndRemoveProduceNothing()
        {
            var add = _changes.Single(c => c.Reason == ChangeReason.Add);
            var remove = _changes.Single(c => c.Reason == ChangeReason.Remove);

            var result = ChangesReducer.Reduce(add, remove);
            result.HasValue.Should().Be(false);
        }

        [Fact]
        public void RemoveAndAddProduceUpdate()
        {
            var remove = _changes.Single(c => c.Reason == ChangeReason.Remove);
            var add = _changes.Single(c => c.Reason == ChangeReason.Add);

            var result = ChangesReducer.Reduce(remove, add);
            var expected = new Change<Person, string>(ChangeReason.Update, _testEntity.Key, add.Current, remove.Current, add.CurrentIndex, remove.CurrentIndex);

            result.Value.Should().Be(expected);
        }
        [Fact]
        public void AddAndUpdateProduceAdd()
        {
            var updatedEntity = new Person(_testEntity.Key, 55);
            var newIndex = _testIndex + 1;

            var add = new Change<Person, string>(ChangeReason.Add, _testEntity.Key, _testEntity, _testIndex);
            var update = new Change<Person, string>(ChangeReason.Update, _testEntity.Key, updatedEntity, add.Current, newIndex, _testIndex);

            var result = ChangesReducer.Reduce(add, update);
            var expected = new Change<Person, string>(ChangeReason.Add, _testEntity.Key, updatedEntity, newIndex);

            result.Value.Should().Be(expected);
        }

        [Fact]
        public void TwoUpdatesProduceUpdate()
        {
            var updatedEntityOne = new Person(_testEntity.Key, 55);
            var updatedEntityTwo = new Person(_testEntity.Key, 33);

            var newIndexOne = _testIndex + 1;
            var newIndexTwo = _testIndex + 2;

            var firstUpdate = new Change<Person, string>(ChangeReason.Update, _testEntity.Key, updatedEntityOne, _testEntity, newIndexOne, _testIndex);
            var secondUpdate = new Change<Person, string>(ChangeReason.Update, _testEntity.Key, updatedEntityTwo, updatedEntityOne, newIndexTwo, newIndexOne);

            var result = ChangesReducer.Reduce(firstUpdate, secondUpdate);
            var expected = new Change<Person, string>(ChangeReason.Update, _testEntity.Key, updatedEntityTwo, _testEntity, newIndexTwo, _testIndex) ;

            result.Value.Should().Be(expected);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public static IEnumerable<object[]> ConstrainFirstValue(ChangeReason constraint, ChangeReason[] othersExcept)
        {
            var constrainedValue = _changes.Single(c => c.Reason == constraint);
            var others = _changes.Where(c => c.Reason != constraint && !othersExcept.Contains(c.Reason));
            return others.Select(other =>
                new object[] {constrainedValue, other});
        }


        // ReSharper disable once MemberCanBePrivate.Global
        public static IEnumerable<object[]> GetChanges()
        {
            return _changes.Select(x=> new object[] { x });
        }
    }
}
