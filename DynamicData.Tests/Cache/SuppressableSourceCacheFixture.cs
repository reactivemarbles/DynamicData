using System;
using DynamicData.Cache;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class SuppressableSourceCacheFixture : IDisposable
    {
        public SuppressableSourceCacheFixture()
        {
            _subjectUnderTest = new SourceCache<string, string>(x => x).WithNotificationSuppressionSupport();
            _result = _subjectUnderTest.Connect().AsAggregator();
        }

        public void Dispose()
        {
            _subjectUnderTest?.Dispose();
        }

        private readonly ISuppressableSourceCache<string, string> _subjectUnderTest;
        private readonly ChangeSetAggregator<string, string> _result;

        [Fact]
        public void ChangesNotSuppressed()
        {
            _subjectUnderTest.Edit(innerCache =>
            {
                innerCache.AddOrUpdate("first");
                innerCache.AddOrUpdate("second");
                innerCache.AddOrUpdate("third");
            });

            3.Should().Be(_result.Data.Count);
        }

        [Fact]
        public void SuppressesNotification()
        {
            using (_subjectUnderTest.SuppressNotifications())
            {
                _subjectUnderTest.Edit(innerCache =>
                {
                    innerCache.AddOrUpdate("first");
                    innerCache.AddOrUpdate("second");
                    innerCache.AddOrUpdate("third");
                });
            }

            0.Should().Be(_result.Data.Count);
        }

        [Fact]
        public void EnableDisableSuppressions()
        {
            using (var inner = _subjectUnderTest.SuppressNotifications())
            {
                inner.Edit(innerCache =>
                {
                    innerCache.AddOrUpdate("first");
                });
            }

            _subjectUnderTest.Edit(innerCache =>
            {
                innerCache.AddOrUpdate("second");
            });

            using (var inner = _subjectUnderTest.SuppressNotifications())
            {
                inner.Edit(innerCache =>
                {
                    innerCache.AddOrUpdate("third");
                });
            }

            1.Should().Be(_result.Data.Count);
        }
    }
}