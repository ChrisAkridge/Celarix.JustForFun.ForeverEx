using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models;

namespace Celarix.JustForFun.ForeverEx.Disassembly
{
    internal static class Disassembler
    {
        private static readonly byte[] operandBuffer = new byte[3];

        public static IEnumerable<DisassembledInstruction> Disassemble(IEnumerable<byte> assembly, AssemblySource source, long addressStart)
        {
            long address = addressStart;
            var enumerator = assembly.GetEnumerator();

            while (TryDisassembleInstruction(enumerator, source, ref address, out var instruction))
            {
                yield return instruction!;
            }
        }

        private static bool TryDisassembleInstruction(IEnumerator<byte> assemblyEnumerator,
            AssemblySource source,
            ref long address, 
            out DisassembledInstruction? instruction)
        {
            if (!assemblyEnumerator.MoveNext())
            {
                instruction = null;
                return false;
            }

            var opcodeByte = assemblyEnumerator.Current;
            var opcode = GetOpcode(opcodeByte);
            var firstOperandKind = GetFirstOperandKind(opcodeByte);
            var secondOperandKind = GetSecondOperandKind(opcodeByte);

            var operandBytesToRead = GetBytesToReadForOperandKind(firstOperandKind)
                + GetBytesToReadForOperandKind(secondOperandKind);
            var secondOperandOffset = GetBytesToReadForOperandKind(firstOperandKind);

            Array.Clear(operandBuffer);
            for (var i = 0; i < operandBytesToRead; i++)
            {
                if (!assemblyEnumerator.TryGetNext(out var operandByte))
                {
                    instruction = new DisassembledInstruction
                    {
                        IsCurrentInstruction = false,
                        Source = source,
                        Address = address,
                        Opcode = opcodeByte,
                        Mnemonic = "(truncated final instruction)"
                    };
                    return false;
                }

                operandBuffer[i] = operandByte;
            }

            var firstOperand = firstOperandKind switch
            {
                OperandKind.NotPresent => string.Empty,
                OperandKind.ImmediateOrAddress => BitConverter.ToUInt16(operandBuffer, 0).ToString("X4"),
                OperandKind.Register => GetRegisterNameFromNumber(operandBuffer[0]),
                OperandKind.RegisterToRegister => GetRegisterToRegisterNameFromNumber(operandBuffer[0]),
                _ => throw new InvalidOperationException()
            };

            var secondOperand = secondOperandKind switch
            {
                OperandKind.NotPresent => string.Empty,
                OperandKind.ImmediateOrAddress => BitConverter.ToUInt16(operandBuffer, secondOperandOffset).ToString("X4"),
                OperandKind.Register => GetRegisterNameFromNumber(operandBuffer[secondOperandOffset]),
                OperandKind.RegisterToRegister => GetRegisterToRegisterNameFromNumber(operandBuffer[secondOperandOffset]),
                _ => throw new InvalidOperationException()
            };

            instruction = new DisassembledInstruction
            {
                IsCurrentInstruction = false,
                Source = source,
                Address = address,
                Opcode = opcodeByte,
                OperandByte1 = GetOperandByteIfPresent(0, operandBytesToRead),
                OperandByte2 = GetOperandByteIfPresent(1, operandBytesToRead),
                OperandByte3 = GetOperandByteIfPresent(2, operandBytesToRead),
                Mnemonic = $"{opcode} {firstOperand} {secondOperand}"
            };
            address += 1 + operandBytesToRead;
            return true;
        }

        private static string GetOpcode(byte opcodeByte)
        {
            return (opcodeByte % ExecutionCore.InstructionCount) switch
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
        }

        private static OperandKind GetFirstOperandKind(byte opcodeByte)
        {
            return (opcodeByte % ExecutionCore.InstructionCount) switch
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
        }

        private static OperandKind GetSecondOperandKind(byte opcodeByte)
        {
            return (opcodeByte % ExecutionCore.InstructionCount) switch
            {
                0 => OperandKind.NotPresent,
                1 => OperandKind.ImmediateOrAddress,
                >= 2 and <= 33 => OperandKind.NotPresent,
                >= 34 => OperandKind.ImmediateOrAddress,
                _ => throw new InvalidOperationException()
            };
        }

        private static int GetBytesToReadForOperandKind(OperandKind kind) => kind switch
        {
            OperandKind.NotPresent => 0,
            OperandKind.ImmediateOrAddress => 2,
            OperandKind.Register => 1,
            OperandKind.RegisterToRegister => 1,
            _ => throw new InvalidOperationException()
        };

        private static byte? GetOperandByteIfPresent(int index, int operandByteCount)
        {
            if (index < operandByteCount)
            {
                return operandBuffer[index];
            }
            return null;
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
