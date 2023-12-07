using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public class EnsureUniqueKeysFixture: IDisposable
{
    private readonly ISourceCache<Person, string> _source;
    private readonly ChangeSetAggregator<Person, string> _results;

    public EnsureUniqueKeysFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect(suppressEmptyChangeSets: false).EnsureUniqueKeys().AsAggregator();
    }


    [Fact]
    public void UniqueForAdds()
    {
        _source.Edit(innerCache =>
        {
            innerCache.AddOrUpdate(new Person("Me", 20));
            innerCache.AddOrUpdate(new Person("Me", 21));
            innerCache.AddOrUpdate(new Person("Me", 22));
        });

        var message1 = _results.Messages[0];
        message1.Count.Should().Be(1);
        message1.First().Current.Age.Should().Be(22);
        message1.First().Reason.Should().Be(ChangeReason.Add);
    }

    [Fact]
    public void AddAndRemove()
    {
        _source.Edit(innerCache =>
        {
            innerCache.AddOrUpdate(new Person("Me", 20));
            innerCache.AddOrUpdate(new Person("Me", 21));
            innerCache.RemoveKey("Me");
        });

        var message1 = _results.Messages[0];
        message1.Count.Should().Be(0);

    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
