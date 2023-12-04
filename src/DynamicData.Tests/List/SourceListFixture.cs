using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List;

public class SourceListFixture
{
    [Fact]
    public void InitialChangeIsRange()
    {
        var source = new SourceList<string>();
        source.Add("A");
        var changeSets = new List<IChangeSet<string>>();

        source.Connect().Subscribe(changeSets.Add).Dispose();


        changeSets[0].First().Type.Should().Be(ChangeType.Range);
        changeSets[0].First().Range.Index.Should().Be(0);
    }
}
