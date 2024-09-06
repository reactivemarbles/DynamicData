using System;
using DynamicData.Cache.Internal;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using Xunit;

namespace DynamicData.Tests
{
    public class EnumerableIListFixture
    {
        [Fact]
        public void EnumerableIListTests()
        {
            var data = new byte[40];
            var rng = new Random(1234567);
            rng.NextBytes(data);

            var inputData = new byte[39];
            var lastItem = data[^1];
            var firstItem = data[0];
            Array.Copy(data, 1, inputData, 0, 38);
            var listOfRandomFloats = new List<byte>(inputData);
            var fixture = EnumerableIList.Create(listOfRandomFloats);
            fixture.Add(lastItem);
            fixture.Insert(0, firstItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            Assert.True(fixture.IndexOf(lastItem) > 0);

            fixture.Remove(lastItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            fixture.RemoveAt(0);

            Assert.Equal(fixture[10], listOfRandomFloats[10]);

            fixture.Clear();

            Assert.True(fixture.Count == 0);
        }

        [Fact]
        public void ExceptionTests()
        {
            var exSubject = new Subject<Exception>();

            object exceptionRecived = default!;
            exSubject.ObserveOn(ImmediateScheduler.Instance).Subscribe(ex => { exceptionRecived = ex; });
            exSubject.OnNext(new UnspecifiedIndexException());

            Assert.IsType<UnspecifiedIndexException>(exceptionRecived);

            exSubject.OnNext(new KeySelectorException());

            Assert.IsType<KeySelectorException>(exceptionRecived);

            exSubject.OnNext(new MissingKeyException());

            Assert.IsType<MissingKeyException>(exceptionRecived);            
        }
    }
}
