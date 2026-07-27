using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class MonitorStatusFixture
{
    [Fact]
    public void InitialiStatusIsLoadding()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subscription = new Subject<int>().MonitorStatus().Subscribe(
            s =>
            {
                invoked = true;
                status = s;
            });
        invoked.Should().BeTrue();
        status.Should().Be(ConnectionStatus.Pending, "No status has been received");
        subscription.Dispose();
    }

    [Fact]
    public void MultipleInvokesDoNotCallLoadedAgain()
    {
        var invoked = false;
        var invocations = 0;
        var subject = new Subject<int>();
        var subscription = subject.MonitorStatus().Where(status => status == ConnectionStatus.Loaded).Subscribe(
            s =>
            {
                invoked = true;
                invocations++;
            });

        subject.OnNext(1);
        subject.OnNext(1);
        subject.OnNext(1);

        invoked.Should().BeTrue();
        invocations.Should().Be(1, "Status should be ConnectionStatus.Loaded");
        subscription.Dispose();
    }

    [Fact]
    public void SetToError()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subject = new Subject<int>();
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

        invoked.Should().BeTrue();
        status.Should().Be(ConnectionStatus.Errored, "Status should be ConnectionStatus.Faulted");
    }

    [Fact]
    public void SetToLoaded()
    {
        var invoked = false;
        var status = ConnectionStatus.Pending;
        var subject = new Subject<int>();
        var subscription = subject.MonitorStatus().Subscribe(
            s =>
            {
                invoked = true;
                status = s;
            });

        subject.OnNext(1);
        invoked.Should().BeTrue();
        status.Should().Be(ConnectionStatus.Loaded, "Status should be ConnectionStatus.Loaded");
        subscription.Dispose();
    }

    [Fact]
    public void CompletesWhenTheSourceCompletes()
    {
        var statuses = new List<ConnectionStatus>();
        var completed = false;

        using var source = new Subject<int>();
        using var subscription = source.MonitorStatus().Subscribe(statuses.Add, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue("the status stream is finished once the source is");
        statuses.Should().EndWith(ConnectionStatus.Completed);
    }

    [Fact]
    public void DeliversTheErrorAfterReportingIt()
    {
        var statuses = new List<ConnectionStatus>();
        Exception? error = null;

        using var source = new Subject<int>();
        using var subscription = source.MonitorStatus().Subscribe(statuses.Add, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
        statuses.Should().EndWith(ConnectionStatus.Errored);
    }

    [Fact]
    public void ReportsAStatusWhenTheSourceIsAlreadyFinished()
    {
        var statuses = new List<ConnectionStatus>();
        var completed = false;

        using var subscription = Observable.Empty<int>().MonitorStatus().Subscribe(statuses.Add, () => completed = true);

        completed.Should().BeTrue("a terminal event arriving during subscription must not be lost");
        statuses.Should().Equal(ConnectionStatus.Pending, ConnectionStatus.Completed);
    }
}
