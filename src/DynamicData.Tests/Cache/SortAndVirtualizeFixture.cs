using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;



internal class SortAndVirtualizeFixture : IDisposable
{

    private readonly SourceCache<Person, string> _source = new(p => p.Name);


    public void Dispose()
    {
        _source.Dispose();
    }
}
