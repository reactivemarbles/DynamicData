// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal
{
    internal interface IKeySelector<in TObject, out TKey> //: IKeySelector<TObject>
    {
        TKey GetKey(TObject item);
    }
}
