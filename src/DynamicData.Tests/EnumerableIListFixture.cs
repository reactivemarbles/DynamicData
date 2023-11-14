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
            Array.Copy(data, inputData, 39);
            var listOfRandomFloats = new List<byte>(inputData);
            var fixture = EnumerableIList.Create(listOfRandomFloats);
            fixture.Add(lastItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            Assert.True(fixture.IndexOf(lastItem) > 0);

            fixture.Remove(lastItem);

            Assert.Equal(fixture.Count, listOfRandomFloats.Count);

            var valueBeforeRemoval = listOfRandomFloats[0];

            fixture.RemoveAt(0);

            Assert.False(fixture.Contains(valueBeforeRemoval));

            Assert.Equal(fixture[10], listOfRandomFloats[10]);

            fixture.Clear();

            Assert.True(fixture.Count == 0);
        }
    }
}
