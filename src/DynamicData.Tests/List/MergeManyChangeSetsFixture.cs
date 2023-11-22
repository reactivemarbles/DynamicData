using System.Linq;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class MergeManyChangeSetsFixture
{
    [Fact]
    public void MergeManyShouldWork()
    {
        var a = new SourceList<int>();
        var b = new SourceList<int>();
        var c = new SourceList<int>();

        var parent = new SourceList<SourceList<int>>();
        parent.Add(a);
        parent.Add(b);
        parent.Add(c);

        var d = parent.Connect().MergeMany(e => e.Connect().RemoveIndex()).AsObservableList();

        d.Count.Should().Be(0);

        a.Add(1);

        d.Count.Should().Be(1);
        a.Add(2);
        d.Count.Should().Be(2);

        b.Add(3);
        d.Count.Should().Be(3);
        b.Add(5);
        d.Count.Should().Be(4);
        new[] { 1, 2, 3, 5 }.Should().BeEquivalentTo(d.Items);

        b.Clear();

        // Fails below
        d.Count.Should().Be(2);
        new[] { 1, 2 }.Should().BeEquivalentTo(d.Items);

        a.ReplaceAt(0,100);
        new[] { 2, 100 }.Should().BeEquivalentTo(d.Items);


        var f = new SourceList<int>();
        f.AddRange(Enumerable.Range(10,5));
        parent.ReplaceAt(2,f);


        new[] { 2, 100, 10,11,12,13,14 }.Should().BeEquivalentTo(d.Items);
    }
}
