using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models.MemoryHistory
{
    internal sealed class MemoryRangeChanged : MemoryHistoryEvent
    {
        public const byte HistoryEventID = 0x40;

        private readonly byte[] changedBytes;

        public ushort StartAddress { get; }
        public IReadOnlyList<byte> ChangedBytes => changedBytes;

        public MemoryRangeChanged(ushort startAddress, byte[] changedBytes)
        {
            if (changedBytes == null || changedBytes.Length == 0)
            {
                throw new ArgumentException("Changed bytes must be populated with at least 1 byte.", nameof(changedBytes));
            }

            StartAddress = startAddress;
            this.changedBytes = changedBytes;
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(HistoryEventID);
            writer.Write(StartAddress);
            writer.Write(changedBytes.Length);
            writer.Write(changedBytes);
        }

        public override void Apply(byte[] memory)
        {
            Array.Copy(changedBytes, 0, memory, StartAddress, changedBytes.Length);
        }

        internal static MemoryRangeChanged ReadEvent(BinaryReader reader)
        {
            var startAddress = reader.ReadUInt16();
            var changedBytesLength = reader.ReadInt32();
            var changedBytes = reader.ReadBytes(changedBytesLength);

            return new MemoryRangeChanged(startAddress, changedBytes);
        }
    }
}
