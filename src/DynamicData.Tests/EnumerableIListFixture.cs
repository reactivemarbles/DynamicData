#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests
{
    public class EnumerableIListFixture
    {
        [Test]
        public async Task EnumerableIListTests()
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

            await Assert.That(fixture.Count).IsEqualTo(listOfRandomFloats.Count);

            await Assert.That(fixture.IndexOf(lastItem) > 0).IsTrue();

            fixture.Remove(lastItem);

            await Assert.That(fixture.Count).IsEqualTo(listOfRandomFloats.Count);

            fixture.RemoveAt(0);

            await Assert.That(fixture[10]).IsEqualTo(listOfRandomFloats[10]);

            fixture.Clear();

            await Assert.That(fixture.Count == 0).IsTrue();
        }

        [Test]
        public async Task ExceptionTests()
        {
            var exSubject = new ReactiveUI.Primitives.Signals.Signal<Exception>();

            object exceptionRecived = default!;
            exSubject.ObserveOn(Scheduler.Immediate).Subscribe(ex => { exceptionRecived = ex; });
            exSubject.OnNext(new UnspecifiedIndexException());

            await Assert.That(exceptionRecived).IsTypeOf<UnspecifiedIndexException>();

            exSubject.OnNext(new KeySelectorException());

            await Assert.That(exceptionRecived).IsTypeOf<KeySelectorException>();

            exSubject.OnNext(new MissingKeyException());

            await Assert.That(exceptionRecived).IsTypeOf<MissingKeyException>();
        }
    }
}
