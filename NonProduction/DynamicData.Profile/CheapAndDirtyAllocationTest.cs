// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DynamicData.Kernel;
using Xunit;

namespace DynamicData.Profile
{
    public class CheapAndDirtyAllocationTest
    {

        [Fact]
        public void ChangeSetAllocations()
        {
            // Arrange
            var iList = Enumerable.Range(1, 100)
                .Select(j => new Person("P" + j, j))
                .Select(p => new Change<Person, string>(ChangeReason.Add, p.Name, p))
                .ToList();

            var changes = new ChangeSet<Person, string>(iList);

            var startAllocs = GC.GetAllocatedBytesForCurrentThread();

            // Assert
            var i = 0;
            foreach (var item in changes)
            {
                i++;
            }

            var endAllocs = GC.GetAllocatedBytesForCurrentThread();
            var diff = endAllocs - startAllocs;
            Assert.Equal(startAllocs, endAllocs);
            Assert.Equal(iList.Count, i);
        }

        [Fact(Skip = "Allocation is not as originally expected, this test has not been run for some time due to a solution fault in loading the test")]
        public void ChangeSetAllocations2()
        {
            // Arrange
            var iList = Enumerable.Range(1, 100)
                .Select(j => new Person("P" + j, j))
                .Select(p => new Change<Person, string>(ChangeReason.Add, p.Name, p))
                .ToList();

            var changes = new ChangeSet<Person, string>(iList);

            var startAllocs = GC.GetAllocatedBytesForCurrentThread();

            // Act
            EnumerableIList<Change<Person, string>> eIList = changes;

            // Assert
            var i = 0;
            var count = changes.Count;
            foreach (var item in eIList)
            {
                i++;
            }

            var endAllocs = GC.GetAllocatedBytesForCurrentThread();
            var diff = endAllocs - startAllocs;
            Assert.Equal(startAllocs, endAllocs);
            Assert.Equal(iList.Count, i);
        }

        [Fact]
        public void NoAllocations()
        {
            // Arrange
            IList<int> iList = new[] { 1, 2, 3, 4, 5, 6, 7 }.ToImmutableArray();

            // Act
            var eIList = EnumerableIList.Create(iList);
            var startAllocs = GC.GetAllocatedBytesForCurrentThread();

            // Assert
            var i = 0;
            foreach (var item in eIList)
            {
                i++;
            }

            var endAllocs = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(startAllocs, endAllocs);
            Assert.Equal(iList.Count, i);
        }

        [Fact]
        public void WithAllocations()
        {
            // Arrange
            IList<int> iList = new[] { 1, 2, 3, 4, 5, 6, 7 }.ToImmutableArray();

            // Act
            var eIList = EnumerableIList.Create(iList);
            var startAllocs = GC.GetAllocatedBytesForCurrentThread();

            // Assert
            var i = 0;
            foreach (var item in eIList)
            {
                i++;
            }

            var endAllocs = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(startAllocs, endAllocs);
            Assert.Equal(iList.Count, i);
        }
    }
}
