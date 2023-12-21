using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models
{
    internal sealed class PinAddressSetEventArgs : EventArgs
    {
        public ushort PinnedAddress { get; set; }
    }
}
