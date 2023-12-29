using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models.MemoryHistory
{
    internal sealed class ROMBankSwitched : MemoryHistoryEvent
    {
        public const byte HistoryEventID = 0x20;    

        private readonly byte[] romBank;

        public IReadOnlyList<byte> ROMBank => romBank;

        public ROMBankSwitched(byte[] romBank)
        {
            if (romBank?.Length != 32768)
            {
                throw new ArgumentException("ROM bank must be 32768 bytes long.", nameof(romBank));
            }

            this.romBank = romBank;
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(HistoryEventID);
            writer.Write(romBank);
        }

        public override void Apply(byte[] memory)
        {
            Array.Copy(romBank, 0, memory, 0x8000, 32768);
        }

        internal static ROMBankSwitched ReadEvent(BinaryReader reader)
        {
            var romBank = reader.ReadBytes(32768);

            return new ROMBankSwitched(romBank);
        }
    }
}
