using System.Diagnostics;

namespace Medallion.Collections;

public sealed partial class BPlusTreeImmutableList<T>
{
    [Conditional("DEBUG")]
    private static void AssertValid(Array root, int count, bool allowMutable = false)
    {

    }
}
