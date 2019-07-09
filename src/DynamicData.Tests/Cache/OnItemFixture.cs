using System;
using DynamicData.Tests.Domain;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class OnItemFixture
    {
        [Fact]
        public void OnItemAddCalled()
        {
            var called = false;
            var source = new SourceCache<Person, int>(x => x.Age);

            source.Connect()
                .OnItemAdded(_ => called = true)
                .Subscribe();

            var person = new Person("A", 1);
            
            source.AddOrUpdate(person);
            Assert.True(called);
        }

        [Fact]
        public void OnItemRemovedCalled()
        {
            var called = false;
            var source = new SourceCache<Person, int>(x => x.Age);

            source.Connect()
                .OnItemRemoved(_ => called = true)
                .Subscribe();

            var person = new Person("A", 1);
            source.AddOrUpdate(person);
            source.Remove(person);
            Assert.True(called);
        }

        [Fact]
        public void OnItemUpdatedCalled()
        {
            var called = false;
            var source = new SourceCache<Person, int>(x => x.Age);

            source.Connect()
                .OnItemUpdated((x,y) => called = true)
                .Subscribe();

            var person = new Person("A", 1);
            source.AddOrUpdate(person);
            var update = new Person("B", 1);
            source.AddOrUpdate(update);
            Assert.True(called);
        }
    }
}