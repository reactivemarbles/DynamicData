using System;
using DynamicData.Binding;
using DynamicData.Controllers;
using NUnit.Framework;
using DynamicData;

namespace DynamicData.Tests.CacheFixtures
{
    public class Widget
    {
        public int Id { get; set; }
        public bool Active { get; set; }
    }

    public class WidgetWrapper :AbstractNotifyPropertyChanged , IDisposable
    {
        public bool IsDisposed { get; private set; }
        public Widget Wrapped { get; private set; }
        public WidgetWrapper(Widget Target)
        {
            IsDisposed = false;
            Wrapped = Target;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public class ThrowawayTest
    {

        [Test]
        public void MoreCoolSolution()
        {
            var subject = new SourceCache<Widget, int>(w => w.Id);

            var activeObservable = new ObservableCollectionExtended<WidgetWrapper>();
            var inactiveObservable = new ObservableCollectionExtended<WidgetWrapper>();

            var activeFilter = new FilterController<WidgetWrapper>(wrapper => wrapper.Wrapped.Active);
            var inactiveFilter = new FilterController<WidgetWrapper>(wrapper => !wrapper.Wrapped.Active);

            //nothing gets removed from here
            var converted = subject
                .Connect()
                .Transform(w => new WidgetWrapper(w))
                .DisposeMany()
                .AsObservableCache();

            var active = converted.Connect()
                   .Filter(activeFilter)
                   .Bind(activeObservable)
                   .Subscribe();

            var inactive = converted.Connect()
                   .Filter(inactiveFilter)
                   .Bind(inactiveObservable)
                   .Subscribe();


            //var trigger = converted.Connect()
            //                .WhenValueChanged(w=>w.IsDisposed)

            var w1 = new Widget() { Id = 0, Active = true };
            var w2 = new Widget() { Id = 1, Active = true };

            subject.AddOrUpdate(w1);
            subject.AddOrUpdate(w2);
            Assert.AreEqual(2, activeObservable.Count);
            Assert.AreEqual(0, inactiveObservable.Count);
            Assert.IsFalse(activeObservable[0].IsDisposed);
            Assert.IsFalse(activeObservable[1].IsDisposed);

            //This needs to be done twice to trigger the behavior
            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            Assert.AreEqual(2, activeObservable.Count);

            Assert.False(activeObservable[0].IsDisposed);
            Assert.False(activeObservable[1].IsDisposed);
        }


        [Test]
        public void Does_Not_Reuse_Disposed_Wrappers_FIXED()
        {
            var subject = new SourceCache<Widget, int>(w => w.Id);

            var activeObservable = new ObservableCollectionExtended<WidgetWrapper>();
            var inactiveObservable = new ObservableCollectionExtended<WidgetWrapper>();

            var activeFilter = new FilterController<WidgetWrapper>(wrapper => wrapper.Wrapped.Active);
            var inactiveFilter = new FilterController<WidgetWrapper>(wrapper => !wrapper.Wrapped.Active);

            //nothing gets removed from here
            var converted = subject
                .Connect()
                .Transform(w => new WidgetWrapper(w))
                .DisposeMany()
                .AsObservableCache();

            var active = converted.Connect()
                   .Filter(activeFilter)
                   .Bind(activeObservable)
                   .Subscribe();

            var inactive = converted.Connect()
                   .Filter(inactiveFilter)
                   .Bind(inactiveObservable)
                   .Subscribe();

            var w1 = new Widget() { Id = 0, Active = true };
            var w2 = new Widget() { Id = 1, Active = true };

            subject.AddOrUpdate(w1);
            subject.AddOrUpdate(w2);
            Assert.AreEqual(2, activeObservable.Count);
            Assert.AreEqual(0, inactiveObservable.Count);
            Assert.IsFalse(activeObservable[0].IsDisposed);
            Assert.IsFalse(activeObservable[1].IsDisposed);

            //This needs to be done twice to trigger the behavior
            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            Assert.AreEqual(2, activeObservable.Count);

            Assert.False(activeObservable[0].IsDisposed);
            Assert.False(activeObservable[1].IsDisposed);
        }


        [Test]
        public void Does_Not_Reuse_Disposed_Wrappers()
        {
            var subject = new SourceCache<Widget, int>(w => w.Id);

            var activeObservable = new ObservableCollectionExtended<WidgetWrapper>();
            var inactiveObservable = new ObservableCollectionExtended<WidgetWrapper>();

            var activeFilter = new FilterController<WidgetWrapper>(wrapper => wrapper.Wrapped.Active);
            var inactiveFilter = new FilterController<WidgetWrapper>(wrapper => !wrapper.Wrapped.Active);

            subject.Connect()
                   .Transform(w => new WidgetWrapper(w))
                   .Filter(activeFilter)
                   .Bind(activeObservable)
                   .DisposeMany()
                   .Subscribe();

            subject.Connect()
                   .Transform(w => new WidgetWrapper(w))
                   .Filter(inactiveFilter)
                   .Bind(inactiveObservable)
                   .DisposeMany()
                   .Subscribe();

            var w1 = new Widget() { Id = 0, Active = true };
            var w2 = new Widget() { Id = 1, Active = true };

            subject.AddOrUpdate(w1);
            subject.AddOrUpdate(w2);
            Assert.AreEqual(2, activeObservable.Count);
            Assert.AreEqual(0, inactiveObservable.Count);
            Assert.IsFalse(activeObservable[0].IsDisposed);
            Assert.IsFalse(activeObservable[1].IsDisposed);

            //This needs to be done twice to trigger the behavior
            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            w1.Active = !w1.Active;
            activeFilter.Reevaluate();
            inactiveFilter.Reevaluate();

            Assert.AreEqual(2, activeObservable.Count);

            Assert.False(activeObservable[0].IsDisposed);
            Assert.False(activeObservable[1].IsDisposed);
        }

    }
}
