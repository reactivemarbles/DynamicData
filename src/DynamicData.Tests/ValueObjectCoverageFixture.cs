#if REACTIVE_TESTS
using DynamicData.Reactive;
using DynamicData.Reactive.Kernel;
using DynamicData.Reactive.Operators;
#else
using DynamicData.Kernel;
using DynamicData.Operators;
#endif

namespace DynamicData.Tests;

public class ValueObjectCoverageFixture
{
    [Test]
    public async Task PageResponseExposesValuesAndComparesByConcreteResponseValues()
    {
        var first = new PageResponse(pageSize: 25, totalSize: 101, page: 2, pages: 5);
        var same = new PageResponse(pageSize: 25, totalSize: 101, page: 2, pages: 5);
        var different = new PageResponse(pageSize: 10, totalSize: 101, page: 2, pages: 5);
        IPageResponse sameValuesDifferentType = new ExternalPageResponse(25, 101, 2, 5);

        await Assert.That(first.PageSize).IsEqualTo(25);
        await Assert.That(first.TotalSize).IsEqualTo(101);
        await Assert.That(first.Page).IsEqualTo(2);
        await Assert.That(first.Pages).IsEqualTo(5);
        await Assert.That(first.Equals(same)).IsTrue();
        await Assert.That(first.Equals((object)same)).IsTrue();
        await Assert.That(first.Equals(different)).IsFalse();
        await Assert.That(first.Equals(null)).IsFalse();
        await Assert.That(first.Equals(new object())).IsFalse();
        await Assert.That(PageResponse.DefaultComparer.Equals(first, first)).IsTrue();
        await Assert.That(PageResponse.DefaultComparer.Equals(null, first)).IsFalse();
        await Assert.That(PageResponse.DefaultComparer.Equals(first, null)).IsFalse();
        await Assert.That(PageResponse.DefaultComparer.Equals(first, sameValuesDifferentType)).IsFalse();
        var hashCode = PageResponse.DefaultComparer.GetHashCode(first!);
        await Assert.That(hashCode).IsEqualTo(PageResponse.DefaultComparer.GetHashCode(same));
        var nullHashCode = PageResponse.DefaultComparer.GetHashCode(null!);
        await Assert.That(nullHashCode).IsEqualTo(0);
        await Assert.That(first.ToString()).IsEqualTo("Page: 2, PageSize: 25, Pages: 5, TotalSize: 101");
    }

    [Test]
    public async Task VirtualResponseExposesValuesAndComparesByConcreteResponseValues()
    {
        var first = new VirtualResponse(size: 25, startIndex: 50, totalSize: 101);
        var same = new VirtualResponse(size: 25, startIndex: 50, totalSize: 101);
        var different = new VirtualResponse(size: 10, startIndex: 50, totalSize: 101);
        IVirtualResponse sameValuesDifferentType = new ExternalVirtualResponse(25, 50, 101);

        await Assert.That(first.Size).IsEqualTo(25);
        await Assert.That(first.StartIndex).IsEqualTo(50);
        await Assert.That(first.TotalSize).IsEqualTo(101);
        await Assert.That(first.Equals(same)).IsTrue();
        await Assert.That(first.Equals((object)same)).IsTrue();
        await Assert.That(first.Equals(different)).IsFalse();
        await Assert.That(first.Equals(null)).IsFalse();
        await Assert.That(first.Equals(new object())).IsFalse();
        await Assert.That(VirtualResponse.DefaultComparer.Equals(first, first)).IsTrue();
        await Assert.That(VirtualResponse.DefaultComparer.Equals(null, first)).IsFalse();
        await Assert.That(VirtualResponse.DefaultComparer.Equals(first, null)).IsFalse();
        await Assert.That(VirtualResponse.DefaultComparer.Equals(first, sameValuesDifferentType)).IsFalse();
        var hashCode = VirtualResponse.DefaultComparer.GetHashCode(first!);
        await Assert.That(hashCode).IsEqualTo(VirtualResponse.DefaultComparer.GetHashCode(same));
        var nullHashCode = VirtualResponse.DefaultComparer.GetHashCode(null!);
        await Assert.That(nullHashCode).IsEqualTo(0);
        await Assert.That(first.ToString()).IsEqualTo("Size: 25, StartIndex: 50, TotalSize: 101");
    }

    [Test]
    public async Task ErrorComparesKeyValueAndExceptionAndFormatsState()
    {
        var exception = new InvalidOperationException("Broken");
        var first = new Error<string, int>(exception, "value", 42);
        var same = new Error<string, int>(exception, "value", 42);
        var differentKey = new Error<string, int>(exception, "value", 43);
        var differentValue = new Error<string, int>(exception, "other", 42);
        var differentException = new Error<string, int>(new InvalidOperationException("Broken"), "value", 42);

        await Assert.That(first.Exception).IsSameReferenceAs(exception);
        await Assert.That(first.Value).IsEqualTo("value");
        await Assert.That(first.Key).IsEqualTo(42);
        await Assert.That(first == same).IsTrue();
        await Assert.That(first != same).IsFalse();
        await Assert.That(first.Equals(first)).IsTrue();
        await Assert.That(first.Equals(same)).IsTrue();
        await Assert.That(first.Equals((object)same)).IsTrue();
        await Assert.That(first.Equals((Error<string, int>?)null)).IsFalse();
        await Assert.That(first.Equals((object?)null)).IsFalse();
        await Assert.That(first.Equals(new object())).IsFalse();
        await Assert.That(first.Equals(differentKey)).IsFalse();
        await Assert.That(first.Equals(differentValue)).IsFalse();
        await Assert.That(first.Equals(differentException)).IsFalse();
        await Assert.That(first.GetHashCode()).IsEqualTo(same.GetHashCode());
        await Assert.That(first.ToString()).IsEqualTo($"Key: 42, Value: value, Exception: {exception}");
    }

    private sealed class ExternalPageResponse(int pageSize, int totalSize, int page, int pages) : IPageResponse
    {
        public int Page { get; } = page;

        public int Pages { get; } = pages;

        public int PageSize { get; } = pageSize;

        public int TotalSize { get; } = totalSize;
    }

    private sealed class ExternalVirtualResponse(int size, int startIndex, int totalSize) : IVirtualResponse
    {
        public int Size { get; } = size;

        public int StartIndex { get; } = startIndex;

        public int TotalSize { get; } = totalSize;
    }
}
