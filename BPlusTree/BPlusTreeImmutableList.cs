using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree.Tests
{
    using static Storage;

    public static class BPlusTreeImmutableList
    {
        public static BPlusTreeImmutableList<T> CreateRange<T>(IEnumerable<T> items) =>
            BPlusTreeImmutableList<T>.Empty.AddRange(items);
    }
}
