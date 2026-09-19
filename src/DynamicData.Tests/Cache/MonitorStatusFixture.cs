#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Cache;

public class MonitorStatusFixture
{
    [Test]
    public async Task InitialiStatusIsLoadding()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subscription = new ReactiveUI.Primitives.Signals.Signal<int>().MonitorStatus().Subscribe(
            s =>
            {
                invoked = true;
                status = s;
            });
        await Assert.That(invoked).IsTrue();
        await Assert.That(status).IsEqualTo(ConnectionStatus.Pending).Because("No status has been received");
        subscription.Dispose();
    }

    [Test]
    public async Task MultipleInvokesDoNotCallLoadedAgain()
    {
        var invoked = false;
        var invocations = 0;
        var subject = new ReactiveUI.Primitives.Signals.Signal<int>();
        var subscription = subject.MonitorStatus().Where(status => status == ConnectionStatus.Loaded).Subscribe(
            s =>
            {
                invoked = true;
                invocations++;
            });

        subject.OnNext(1);
        subject.OnNext(1);
        subject.OnNext(1);

        await Assert.That(invoked).IsTrue();
        await Assert.That(invocations).IsEqualTo(1).Because("Status should be ConnectionStatus.Loaded");
        subscription.Dispose();
    }

    [Test]
    public async Task SetToError()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subject = new ReactiveUI.Primitives.Signals.Signal<int>();
        Exception exception;

        var subscription = subject.MonitorStatus().Subscribe(
            s =>
            {
                invoked = true;
                status = s;
            },
            ex => { exception = ex; });

        subject.OnError(new Exception("Test"));
        subscription.Dispose();

        await Assert.That(invoked).IsTrue();
        await Assert.That(status).IsEqualTo(ConnectionStatus.Errored).Because("Status should be ConnectionStatus.Faulted");
    }

    [Test]
    public async Task SetToLoaded()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subject = new ReactiveUI.Primitives.Signals.Signal<int>();
        var subscription = subject.MonitorStatus().Subscribe(
            s =>
            {
                invoked = true;
                status = s;
            });

        subject.OnNext(1);
        await Assert.That(invoked).IsTrue();
        await Assert.That(status).IsEqualTo(ConnectionStatus.Loaded).Because("Status should be ConnectionStatus.Loaded");
        subscription.Dispose();
    }
}
