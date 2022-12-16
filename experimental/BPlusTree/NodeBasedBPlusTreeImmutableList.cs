using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    using static Storage;

    public static class NodeBasedBPlusTreeImmutableList
    {
        public static NodeBasedBPlusTreeImmutableList<T> CreateRange<T>(IEnumerable<T> items) =>
            NodeBasedBPlusTreeImmutableList<T>.Empty.AddRange(items);
    }
}
