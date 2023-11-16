using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List;

public class ToCollectionFixture
{
    [Fact]
    public void ToCollectionTest()
    {
        var list = new SourceList<string>();
        //   var collection = Observable.Defer(() =>  list.Connect().ToCollection());
        var collection = list.Connect().ToCollection();
        IReadOnlyCollection<string>? res1 = null;
        IReadOnlyCollection<string>? res2 = null;
        collection.Subscribe(x => res1 = x);
        collection.Subscribe(x => res2 = x);
        list.Add("1");
        list.Add("2");
        res1?.Count.Should().Be(2);
        res2?.Count.Should().Be(2);
    }
}
