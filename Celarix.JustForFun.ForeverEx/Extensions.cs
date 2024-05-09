using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx
{
    internal static class Extensions
    {
        public static bool TryGetNext<T>(this IEnumerator<T> enumerator, out T? result)
        {
            if (enumerator.MoveNext())
            {
                result = enumerator.Current;
                return true;
            }

            result = default;
            return false;
        }
    }
}
