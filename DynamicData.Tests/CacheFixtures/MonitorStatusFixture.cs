using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class MonitorStatusFixture
    {
        [Test]
        public void InitialiStatusIsLoadding()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subscription = new Subject<int>().MonitorStatus().Subscribe(s =>
            {
                invoked = true;
                status = s;
            });
            Assert.IsTrue(invoked, "No status has been received");
            Assert.AreEqual(ConnectionStatus.Pending, status, "No status has been received");
            subscription.Dispose();
        }

        [Test]
        public void SetToLoaded()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subject = new Subject<int>();
            var subscription = subject.MonitorStatus().Subscribe(s =>
            {
                invoked = true;
                status = s;
            });

            subject.OnNext(1);
            Assert.IsTrue(invoked, "No update has been received");
            Assert.AreEqual(ConnectionStatus.Loaded, status, "Status should be ConnectionStatus.Loaded");
            subscription.Dispose();
        }

        [Test]
        public void SetToError()
        {
            bool invoked = false;
            var status = ConnectionStatus.Pending;
            var subject = new Subject<int>();
            Exception exception;

            var subscription = subject.MonitorStatus().Subscribe(s =>
            {
                invoked = true;
                status = s;
            }, ex => { exception = ex; });

            subject.OnError(new Exception("Test"));
            subscription.Dispose();

            Assert.IsTrue(invoked, "No update has been received");
            Assert.AreEqual(ConnectionStatus.Errored, status, "Status should be ConnectionStatus.Faulted");
        }

        [Test]
        public void MultipleInvokesDoNotCallLoadedAgain()
        {
            bool invoked = false;
            int invocations = 0;
            var subject = new Subject<int>();
            var subscription = subject
                .MonitorStatus()
                .Where(status => status == ConnectionStatus.Loaded)
                .Subscribe(s =>
                {
                    invoked = true;
                    invocations++;
                });

            subject.OnNext(1);
            subject.OnNext(1);
            subject.OnNext(1);

            Assert.IsTrue(invoked, "No status has been received");
            Assert.AreEqual(1, invocations, "Status should be ConnectionStatus.Loaded");
            subscription.Dispose();
        }
    }
}
