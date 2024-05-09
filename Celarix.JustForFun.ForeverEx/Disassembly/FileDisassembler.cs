using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx.Disassembly
{
    internal static class FileDisassembler
    {
        public static void DisassembleFile(string romImagePath, string disassemblyOutputPath)
        {
            using var romImageStream = File.OpenRead(romImagePath);
            var romImageBytes = StreamToEnumerable(romImageStream);
            using var outputStream = new StreamWriter(disassemblyOutputPath);
            var disassembly = Disassembler.Disassemble(romImageBytes, AssemblySource.ByteStream, 0);

            foreach (var instruction in disassembly)
            {
                var addressString = instruction.Address.ToString("X8");
                var opcodeString = instruction.Opcode.ToString("X2");
                var operandByte1String = instruction.OperandByte1.HasValue ? instruction.OperandByte1.Value.ToString("X2") : "  ";
                var operandByte2String = instruction.OperandByte2.HasValue ? instruction.OperandByte2.Value.ToString("X2") : "  ";
                var operandByte3String = instruction.OperandByte3.HasValue ? instruction.OperandByte3.Value.ToString("X2") : "  ";
                var opcodeASCII = ByteToHexEditorASCII(instruction.Opcode);
                var operandByte1ASCII = instruction.OperandByte1.HasValue ? ByteToHexEditorASCII(instruction.OperandByte1.Value) : ' ';
                var operandByte2ASCII = instruction.OperandByte2.HasValue ? ByteToHexEditorASCII(instruction.OperandByte2.Value) : ' ';
                var operandByte3ASCII = instruction.OperandByte3.HasValue ? ByteToHexEditorASCII(instruction.OperandByte3.Value) : ' ';
                var mnemonic = instruction.Mnemonic;
                outputStream.WriteLine($"0x{addressString} {opcodeString} {operandByte1String} {operandByte2String} {operandByte3String}  {opcodeASCII} {operandByte1ASCII} {operandByte2ASCII} {operandByte3ASCII}  {mnemonic}");
            }
        }

        private static IEnumerable<byte> StreamToEnumerable(Stream stream)
        {
            // Standard buffer-based approach
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    yield return buffer[i];
                }
            }
        }

        private static char ByteToHexEditorASCII(byte b) =>
            b < 32 || b > 126
                ? '.'
                : (char)b;
    }
}
