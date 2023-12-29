using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models.MemoryHistory
{
    internal sealed class SingleByteChanged : MemoryHistoryEvent
    {
        public const byte HistoryEventID = 0x80;
        
        private ushort AddressChanged { get; }
        private byte NewValue { get; }

        public SingleByteChanged(ushort addressChanged, byte newValue)
        {
            AddressChanged = addressChanged;
            NewValue = newValue;
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(HistoryEventID);
            writer.Write(AddressChanged);
            writer.Write(NewValue);
        }

        public override void Apply(byte[] memory)
        {
            memory[AddressChanged] = NewValue;
        }

        internal static SingleByteChanged ReadEvent(BinaryReader reader)
        {
            var addressChanged = reader.ReadUInt16();
            var newValue = reader.ReadByte();

            return new SingleByteChanged(addressChanged, newValue);
        }
    }
}
