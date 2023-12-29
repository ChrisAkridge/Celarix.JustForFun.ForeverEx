using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models.MemoryHistory
{
    internal abstract class MemoryHistoryEvent
    {
        public abstract void Write(BinaryWriter writer);
        public abstract void Apply(byte[] memory);

        public static MemoryHistoryEvent Read(BinaryReader reader)
        {
            var eventType = reader.ReadByte();

            if (eventType == ROMBankSwitched.HistoryEventID)
            {
                return ROMBankSwitched.ReadEvent(reader);
            }
            else if (eventType == MemoryRangeChanged.HistoryEventID)
            {
                return MemoryRangeChanged.ReadEvent(reader);
            }
            else if (eventType == SingleByteChanged.HistoryEventID)
            {
                return SingleByteChanged.ReadEvent(reader);
            }
            else
            {
                throw new InvalidOperationException($"Unknown memory history event type 0x{eventType:X2}");
            }
        }
    }
}
