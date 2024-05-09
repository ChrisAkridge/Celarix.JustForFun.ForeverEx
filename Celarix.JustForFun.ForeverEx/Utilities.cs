using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx
{
    internal static class Utilities
    {
        public static IEnumerable<byte> StreamToEnumerable(Stream stream)
        {
            // Standard buffer-based approach
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    yield return buffer[i];
                }
            }
        }
    }
}
