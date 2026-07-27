using System;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Merge propagates a failure from any inner stream, rather than discarding it.
/// </summary>
public class MergeManyInnerErrorFixture
{
    [Fact]
    public void MergeManyDeliversErrorFromAChild()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var child = new Subject<int>();
        Exception? error = null;

        using var subscription = source.Connect().MergeMany(_ => child).Subscribe(_ => { }, ex => error = ex, () => { });

        source.AddOrUpdate(new Person("a", 1));
        child.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>("a failing inner stream must not be silently discarded");
    }

    [Fact]
    public void MergeManyItemsDeliversErrorFromAChild()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var child = new Subject<int>();
        Exception? error = null;

        using var subscription = source.Connect().MergeManyItems(_ => child).Subscribe(_ => { }, ex => error = ex, () => { });

        source.AddOrUpdate(new Person("a", 1));
        child.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

}
