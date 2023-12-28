using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverExMemoryView
{
    public static class SharedSynchronization
    {
        private static readonly ManualResetEvent repaintCompleteEvent = new ManualResetEvent(initialState: true);

        public static void SignalRepaintComplete()
        {
            repaintCompleteEvent.Set();
        }

        public static void WaitForRepaintComplete()
        {
            repaintCompleteEvent.WaitOne();
            repaintCompleteEvent.Reset();
        }
    }
}
