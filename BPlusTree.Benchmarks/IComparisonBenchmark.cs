using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TunnelVisionLabs.Collections.Trees.Immutable;

namespace BPlusTree.Benchmarks
{
    internal interface IComparisonBenchmark<T>
    {
        ImmutableList<T> ImmutableList();
        ArrayBasedBPlusTreeImmutableList<T> ArrayBasedImmutableList();
        ImmutableTreeList<T> TunnelVisionImmutableList();
    }
}
