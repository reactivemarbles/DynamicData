using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DynamicData.Kernel;
using Xunit;

namespace DynamicData.Tests
{
    public class EnumerableIListFixture
    {
        [Fact]
        public void EnumerableIListTests()
        {
            var data = new byte[40];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(data);
            }

            var inputData = new byte[39];
            var lastItem = data[data.Length - 1];
            var firstItem = data[0];
            Array.Copy(data, 1, inputData, 0, 38);
            var listOfRandomFloats = new List<byte>(inputData);
            var fixture = EnumerableIList.Create(listOfRandomFloats);
            fixture.Add(lastItem);
            fixture.Insert(0, firstItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            Assert.True(fixture.IndexOf(lastItem) > 0);

            fixture.Remove(lastItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            fixture.RemoveAt(0);

            Assert.False(fixture.IndexOf(firstItem) > 0);

            Assert.Equal(fixture[10], listOfRandomFloats[10]);

            fixture.Clear();

            Assert.True(fixture.Count == 0);
        }
    }
}
