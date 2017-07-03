using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class MonitorStatusFixture
    {
        [Fact]
        public void InitialiStatusIsLoadding()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subscription = new Subject<int>().MonitorStatus().Subscribe(s =>
            {
                invoked = true;
                status = s;
            });
            invoked.Should().BeTrue();
            status.Should().Be(ConnectionStatus.Pending, "No status has been received");
            subscription.Dispose();
        }

        [Fact]
        public void SetToLoaded()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subject = new Subject<int>();
            var subscription = subject.MonitorStatus()
                .Subscribe(s =>
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
        public void SetToError()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subject = new Subject<int>();
            Exception exception;

            var subscription = subject.MonitorStatus()
                .Subscribe(s =>
                {
                    invoked = true;
                    status = s;
                }, ex => { exception = ex; });

            subject.OnError(new Exception("Test"));
            subscription.Dispose();

            invoked.Should().BeTrue();
            status.Should().Be(ConnectionStatus.Errored, "Status should be ConnectionStatus.Faulted");
        }

        [Fact]
        public void MultipleInvokesDoNotCallLoadedAgain()
        {
            bool invoked = false;
            int invocations = 0;
            var subject = new Subject<int>();
            var subscription = subject.MonitorStatus()
                .Where(status => status == ConnectionStatus.Loaded)
                .Subscribe(s =>
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
    }
}
