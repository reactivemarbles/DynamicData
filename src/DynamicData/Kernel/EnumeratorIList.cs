// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.
namespace DynamicData.Kernel
{
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics.")]
    internal struct EnumeratorIList<T> : IEnumerator<T>
    {
        private readonly IList<T> _list;

        private int _index;

        public EnumeratorIList(IList<T> list)
        {
            _index = -1;
            _list = list;
        }

        public T Current => _list[_index];

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _index++;

            return _index < (_list?.Count ?? 0);
        }

        public void Dispose()
        {
        }

        public void Reset() => _index = -1;
    }
}