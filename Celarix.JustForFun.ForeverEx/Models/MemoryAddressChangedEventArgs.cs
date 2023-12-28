using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models
{
    internal sealed class MemoryAddressChangedEventArgs : EventArgs
    {
        public ushort AddressWithChange { get; set; }
        public byte NewValue { get; set; }

        public MemoryAddressChangedEventArgs(ushort addressWithChange, byte newValue)
        {
            AddressWithChange = addressWithChange;
            NewValue = newValue;
        }
    }
}
