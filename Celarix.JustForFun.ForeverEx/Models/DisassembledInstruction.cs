﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Models
{
    internal sealed class DisassembledInstruction
    {
        public bool IsCurrentInstruction { get; set; }
        public ushort Address { get; set; }
        public byte Opcode { get; set; }
        public byte? OperandByte1 { get; set; }
        public byte? OperandByte2 { get; set; }
        public string Mnemonic { get; set; }
    }
}
