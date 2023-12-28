using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models;

namespace Celarix.JustForFun.ForeverEx
{
    internal static class Disassembler
    {
        public static void Disassemble(byte[] assembly, int index, ushort startAddress, DisassembledInstruction[] destination)
        {
            Array.Clear(destination);
            var disassembledBytes = 0;

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = DisassembleInstruction(assembly, index + disassembledBytes, startAddress, out var instructionLength);
                startAddress += (ushort)instructionLength;
                disassembledBytes += instructionLength;
            }
        }

        private static DisassembledInstruction DisassembleInstruction(byte[] assembly, int index, ushort address, out int instructionLength)
        {
            if (index >= assembly.Length)
            {
                instructionLength = 1;
                return new DisassembledInstruction
                {
                    IsCurrentInstruction = false,
                    Address = address,
                    Opcode = 0,
                    Mnemonic = "(out of range)"
                };
            }

            var opcodeByte = assembly[index];
            var opcode = (opcodeByte % ExecutionCore.InstructionCount) switch
            {
                0 => "NOP",
                1 => "IMM",
                2 => "MOV",
                3 => "LDA",
                4 => "LDB",
                5 => "STA",
                6 => "STB",
                7 => "PUSH",
                8 => "POP",
                9 => "ADD",
                10 => "SUB",
                11 => "MUL",
                12 => "DIV",
                13 => "AND",
                14 => "OR",
                15 => "XOR",
                16 => "NOT",
                17 => "CMP",
                18 => "JE",
                19 => "JNE",
                20 => "JLT",
                21 => "JGT",
                22 => "JLTE",
                23 => "JGTE",
                24 => "JE",
                25 => "JNE",
                26 => "JLT",
                27 => "JGT",
                28 => "JLTE",
                29 => "JGTE",
                30 => "WRITE",
                31 => "WRITE",
                32 => "READ",
                33 => "READ",
                34 => "ADD",
                35 => "SUB",
                _ => throw new InvalidOperationException()
            };
            var firstOperandKind = (opcodeByte % ExecutionCore.InstructionCount) switch
            {
                0 => OperandKind.NotPresent,
                1 => OperandKind.Register,
                2 => OperandKind.RegisterToRegister,
                >= 3 and <= 8 => OperandKind.Register,
                >= 9 and <= 17 => OperandKind.NotPresent,
                >= 18 and <= 23 => OperandKind.ImmediateOrAddress,
                >= 24 and <= 29 => OperandKind.Register,
                30 => OperandKind.ImmediateOrAddress,
                31 => OperandKind.Register,
                32 => OperandKind.ImmediateOrAddress,
                >= 33 => OperandKind.Register,
                _ => throw new InvalidOperationException()
            };
            var secondOperandKind = (opcodeByte % ExecutionCore.InstructionCount) switch
            {
                0 => OperandKind.NotPresent,
                1 => OperandKind.ImmediateOrAddress,
                >= 2 and <= 33 => OperandKind.NotPresent,
                >= 34 => OperandKind.ImmediateOrAddress,
                _ => throw new InvalidOperationException()
            };
            // TODO: THESE ARE AWFUL
            // NO GUARD FOR OUT OF RANGE
            // BUILD THE UINT16 MANUALLY
            string firstOperand = firstOperandKind switch
            {
                OperandKind.NotPresent => string.Empty,
                OperandKind.ImmediateOrAddress => BitConverter.ToUInt16(assembly, index + 1).ToString("X4"),
                OperandKind.Register => GetRegisterNameFromNumber(assembly[index + 1]),
                OperandKind.RegisterToRegister => GetRegisterToRegisterNameFromNumber(assembly[index + 1]),
                _ => throw new InvalidOperationException()
            };

            string secondOperand = secondOperandKind switch
            {
                OperandKind.NotPresent => string.Empty,
                OperandKind.ImmediateOrAddress => BitConverter.ToUInt16(assembly, index + 1).ToString("X4"),
                OperandKind.Register => GetRegisterNameFromNumber(assembly[index + 1]),
                OperandKind.RegisterToRegister => GetRegisterToRegisterNameFromNumber(assembly[index + 1]),
                _ => throw new InvalidOperationException()
            };

            instructionLength = 1
                + firstOperandKind switch
                {
                    OperandKind.NotPresent => 0,
                    OperandKind.ImmediateOrAddress => 2,
                    OperandKind.Register => 1,
                    OperandKind.RegisterToRegister => 1,
                    _ => throw new InvalidOperationException()
                }
                + secondOperandKind switch
                {
                    OperandKind.NotPresent => 0,
                    OperandKind.ImmediateOrAddress => 2,
                    OperandKind.Register => 1,
                    OperandKind.RegisterToRegister => 1,
                    _ => throw new InvalidOperationException()
                };

            return new DisassembledInstruction
            {
                IsCurrentInstruction = false,
                Address = address,
                Opcode = opcodeByte,
                OperandByte1 = firstOperandKind switch
                {
                    OperandKind.NotPresent => null,
                    OperandKind.ImmediateOrAddress => assembly[index + 1],
                    OperandKind.Register => assembly[index + 1],
                    OperandKind.RegisterToRegister => assembly[index + 1],
                    _ => throw new InvalidOperationException()
                },
                OperandByte2 = secondOperandKind switch
                {
                    OperandKind.NotPresent => null,
                    OperandKind.ImmediateOrAddress => assembly[index + 2],
                    OperandKind.Register => assembly[index + 2],
                    OperandKind.RegisterToRegister => assembly[index + 2],
                    _ => throw new InvalidOperationException()
                },
                OperandByte3 = secondOperandKind switch
                {
                    OperandKind.NotPresent => null,
                    OperandKind.ImmediateOrAddress => assembly[index + 3],
                    OperandKind.Register => assembly[index + 3],
                    OperandKind.RegisterToRegister => assembly[index + 3],
                    _ => throw new InvalidOperationException()
                },
                Mnemonic = $"{opcode} {firstOperand} {secondOperand}".Trim()
            };
        }

        private static string GetRegisterNameFromNumber(byte registerNumber) => (registerNumber & 0b111) switch
        {
            0 => "A",
            1 => "B",
            2 => "X",
            3 => "Y",
            4 => "IP",
            5 => "SP",
            6 => "BANKNUM",
            7 => "FLAGS",
            _ => throw new InvalidOperationException()
        };

        private static string GetRegisterToRegisterNameFromNumber(byte registerToRegisterNumber)
        {
            var sourceBits = (registerToRegisterNumber & 0b111000) >> 3;
            var destinationBits = registerToRegisterNumber & 0b111;
            var source = GetRegisterNameFromNumber((byte)sourceBits);
            var destination = GetRegisterNameFromNumber((byte)destinationBits);
            return $"{source} {destination}";
        }
    }
}
