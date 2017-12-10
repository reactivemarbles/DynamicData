using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Playground
{
    public class Issue104
    {
        [Fact]
        public void ReadonlyCollectionDynamicData()
        {
            var scheduler = new TestScheduler();
            var list = new SourceList<int>();
            var filterFunc = new BehaviorSubject<Func<int, bool>>(x => true);

            list.Connect()
                //if you comment out the filter or the observeon then this passes
                .Filter(filterFunc.ObserveOn(scheduler))
                .ObserveOn(scheduler)
                .Bind(out var oc)
                .Subscribe();

            list.Edit(updateAction =>
            {
                updateAction.Add(1);
                updateAction.Add(2);
                updateAction.Add(3);
            });

            scheduler.Start();
            oc.Count.Should().Be(3);

            list.Edit(updateAction =>
            {
                updateAction.Remove(1);
                updateAction.Remove(2);
                updateAction.Remove(3);

                updateAction.Count.Should().Be(0);

                updateAction.Add(4);
                updateAction.Add(5);
                updateAction.Add(6);
            });

            scheduler.Start();

            //This fails and those other update actions aren't removed 
            oc.Count.Should().Be(3);
        }
    }
}
