using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models
{
    internal sealed class ConsoleOutputWrittenEventArgs : EventArgs
    {
        public string WrittenOutput { get; set; }
    }
}
