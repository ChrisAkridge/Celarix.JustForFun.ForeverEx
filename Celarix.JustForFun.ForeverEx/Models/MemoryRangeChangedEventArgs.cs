using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models
{
    internal sealed class MemoryRangeChangedEventArgs : EventArgs
    {
        public ushort ChangedRangeStartAddress { get; }
        public int ChangedRangeLength { get; }

        public MemoryRangeChangedEventArgs(ushort changedRangeStartAddress, int changedRangeLength)
        {
            ChangedRangeStartAddress = changedRangeStartAddress;
            ChangedRangeLength = changedRangeLength;
        }
    }
}
