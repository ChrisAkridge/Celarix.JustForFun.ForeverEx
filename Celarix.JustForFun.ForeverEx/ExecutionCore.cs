using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models;

namespace Celarix.JustForFun.ForeverEx
{
    internal sealed class ExecutionCore
    {
        #region RAM and ROM
        private const int RAMSize = 32768;
        private const int ROMBankSize = 32768;
        private const int MappedROMTotalSize = 32768 * 16;

        private byte[] ram = new byte[RAMSize];

        private ROMMappingMode romMappingMode;
        private byte[] currentROMBank = new byte[ROMBankSize];
        // For Mapped16 mode
        private byte[] completeROM = new byte[MappedROMTotalSize];
        // For OverflowShifting mode
        private FileStream romImage;
        private long romBankOffset;
        #endregion

        private const int InstructionCount = 33;

        #region Registers
        private ushort a;
        private ushort b;
        private ushort x;
        private ushort y;
        private ushort sp;
        private ushort ip;
        private byte bankNum;

        private byte BankNum
        {
            get => bankNum;
            set
            {
                bankNum = value;
                SwitchBank(bankNum);
            }
        }

        private byte flags;

        private bool Equals => (flags & 7) == 1;
        private bool LessThan => (flags & 7) == 4;
        private bool GreaterThan => (flags & 7) == 2;
        private bool LessThanOrEqualTo => (flags & 7) == 5;
        private bool GreaterThanOrEqualTo => (flags & 7) == 3;
        private bool NotEquals => (flags & 7) == 0;
        #endregion

        public event EventHandler<ConsoleOutputWrittenEventArgs> ConsoleOutputWritten;
        public Func<string> getInput;

        public ExecutionCore(string romImagePath,
            ROMMappingMode romMappingMode,
            Func<string> getInput)
        {
            this.romMappingMode = romMappingMode;

            if (romMappingMode == ROMMappingMode.Mapped16)
            {
                using var romImageReader = new BinaryReader(File.OpenRead(romImagePath));
                romImageReader.Read(completeROM, 0, MappedROMTotalSize);
            }
            else
            {
                romImage = File.OpenRead(romImagePath);
            }

            SwitchBank(0);
            ip = 0x8000;
            this.getInput = getInput;
        }

        public void ExecuteSingleInstruction()
        {
            var opcode = ReadByteAtAddress(ip) % InstructionCount;
        }

        private void SwitchBank(int bankNumber)
        {
            if (romMappingMode == ROMMappingMode.Mapped16)
            {
                Array.Copy(completeROM, bankNumber * ROMBankSize, currentROMBank, 0, ROMBankSize);
            }
            else
            {
                romBankOffset = bankNumber * ROMBankSize;
                romImage.Seek(romBankOffset, SeekOrigin.Begin);
                romImage.Read(currentROMBank, 0, ROMBankSize);
            }
        }

        private void PreviousBank()
        {
            if (romMappingMode == ROMMappingMode.Mapped16)
            {
                SwitchBank(BankNum == 0 ? 15 : BankNum - 1);
            }
            else
            {
                romBankOffset -= ROMBankSize;
                romImage.Seek(romBankOffset, SeekOrigin.Begin);
                romImage.Read(currentROMBank, 0, ROMBankSize);
            }
        }

        private void NextBank()
        {
            if (romMappingMode == ROMMappingMode.Mapped16)
            {
                SwitchBank(BankNum == 15 ? 0 : BankNum + 1);
            }
            else
            {
                romBankOffset += ROMBankSize;
                romImage.Seek(romBankOffset, SeekOrigin.Begin);
                romImage.Read(currentROMBank, 0, ROMBankSize);
            }
        }

        private byte ReadByteAtAddress(ushort address) =>
            address < 0x8000 ? ram[address] : currentROMBank[address - 0x8000];

        private void WriteByteAtAddress(ushort address, byte value)
        {
            if (address < 0x8000)
            {
                ram[address] = value;
            }

            // Permit "writes" to ROM that don't actually do anything
        }

        private void SetRegister(int registerNumber, ushort value)
        {
            if (registerNumber == 0) { a = value; }
            else if (registerNumber == 1) { b = value; }
            else if (registerNumber == 2) { x = value; }
            else if (registerNumber == 3) { y = value; }
            else if (registerNumber == 4) { sp = value; }
            else if (registerNumber == 5) { ip = value; }
            else if (registerNumber == 6) { BankNum = (byte)value; }
            else if (registerNumber == 7) { flags = (byte)value; }
            else { throw new ArgumentOutOfRangeException(nameof(registerNumber)); }
        }

        public ushort GetRegister(int registerNumber) =>
            registerNumber switch
            {
                0 => a,
                1 => b,
                2 => x,
                3 => y,
                4 => sp,
                5 => ip,
                6 => BankNum,
                7 => flags,
                _ => throw new ArgumentOutOfRangeException(nameof(registerNumber))
            };

        private ushort NextAddressForSP(ushort address) =>
            // SP is restricted to 0x0000 to 0x7FFF
            address == 0x7FFF ? (ushort)0x0000 : (ushort)(address + 1);

        private ushort PreviousAddressForSP(ushort address) =>
            // SP is restricted to 0x0000 to 0x7FFF
            address == 0x0000 ? (ushort)0x7FFF : (ushort)(address - 1);

        private ushort NextAddressForIP(ushort address)
        {
            // When just incrementing IP, it stays in the half of the address space it's in
            if (address == 0x7FFF)
            {
                return 0;
            }
            else if (address == 0xFFFF)
            {
                if (romMappingMode == ROMMappingMode.OverflowShifting)
                {
                    NextBank();
                    return 0x8000;
                }
            }
            return (ushort)(address + 1);
        }

        #region Instructions
        public void NoOperation() => ip = NextAddressForIP(ip);

        public void LoadImmediate()
        {
            ushort registerNumberAddress = NextAddressForIP(ip);
            var registerNumber = ReadByteAtAddress(registerNumberAddress);

            ushort valueLowAddress = NextAddressForIP(registerNumberAddress);
            var valueLow = ReadByteAtAddress(valueLowAddress);

            ushort valueHighAddress = NextAddressForIP(registerNumberAddress);
            var valueHigh = ReadByteAtAddress(valueHighAddress);

            ushort immediate = (ushort)((valueHigh << 8) | valueLow);
            SetRegister(registerNumber, immediate);
            ip = NextAddressForIP(valueHighAddress);
        }

        public void MoveRegisterValue()
        {
            ushort regToRegAddress = NextAddressForIP(ip);
            var regToReg = ReadByteAtAddress(regToRegAddress);
            var source = (regToReg & 0b0011_1000) >> 3;
            var destination = regToReg & 0b0000_0111;
            SetRegister(destination, GetRegister(source));
            ip = NextAddressForIP(regToRegAddress);
        }

        public void LoadA()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            a = (ushort)(ReadByteAtAddress(address) | (ReadByteAtAddress((ushort)(address + 1)) << 8));
            ip = NextAddressForIP(regNumAddress);
        }

        public void LoadB()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            b = (ushort)(ReadByteAtAddress(address) | (ReadByteAtAddress((ushort)(address + 1)) << 8));
            ip = NextAddressForIP(regNumAddress);
        }

        public void StoreA()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            WriteByteAtAddress(address, (byte)(a & 0xFF));
            WriteByteAtAddress((ushort)(address + 1), (byte)((a & 0xFF00) >> 8));
            ip = NextAddressForIP(regNumAddress);
        }

        public void StoreB()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            WriteByteAtAddress(address, (byte)(b & 0xFF));
            WriteByteAtAddress((ushort)(address + 1), (byte)((b & 0xFF00) >> 8));
            ip = NextAddressForIP(regNumAddress);
        }

        public void PushToStack()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var registerValue = GetRegister(regNum);
            WriteByteAtAddress(sp, (byte)(registerValue & 0xFF));
            WriteByteAtAddress(NextAddressForSP(sp), (byte)((registerValue & 0xFF00) >> 8));
            ip = NextAddressForIP(regNumAddress);
        }

        public void PopFromStack()
        {
            ushort regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var registerValueLow = ReadByteAtAddress(sp);
            var registerValueHigh = ReadByteAtAddress(NextAddressForSP(sp));
            SetRegister(regNum, (ushort)((registerValueHigh << 8) | registerValueLow));
            ip = NextAddressForIP(regNumAddress);
        }

        public void MathOperation(MathOperationKind kind)
        {
            // Memory layout:
            // AAAA BBBB cccc
            //           ^ sp

            var bHighAddress = PreviousAddressForSP(sp);
            var bLowAddress = PreviousAddressForSP(bHighAddress);
            var aHighAddress = PreviousAddressForSP(bLowAddress);
            var aLowAddress = PreviousAddressForSP(aHighAddress);

            var b = (ushort)((ReadByteAtAddress(bHighAddress) << 8) | ReadByteAtAddress(bLowAddress));
            var a = (ushort)((ReadByteAtAddress(aHighAddress) << 8) | ReadByteAtAddress(aLowAddress));

            var result = kind switch
            {
                MathOperationKind.Add => (ushort)(a + b),
                MathOperationKind.Subtract => (ushort)(a - b),
                MathOperationKind.Multiply => (ushort)(a * b),
                MathOperationKind.Divide => b != 0
                    ? (ushort)(a / b)
                    : (ushort)0xFFFF,
                MathOperationKind.BitwiseAnd => (ushort)(a & b),
                MathOperationKind.BitwiseOr => (ushort)(a | b),
                MathOperationKind.BitwiseXor => (ushort)(a ^ b),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            sp = PreviousAddressForSP(aLowAddress);
            WriteByteAtAddress(sp, (byte)(result & 0xFF));
            WriteByteAtAddress(NextAddressForSP(sp), (byte)((result & 0xFF00) >> 8));
            sp = NextAddressForSP(sp);
            sp = NextAddressForSP(sp);

            ip = NextAddressForIP(ip);
        }

        public void BitwiseNOT()
        {
            var operandHighAddress = PreviousAddressForSP(sp);
            var operandLowAddress = PreviousAddressForSP(operandHighAddress);

            var operand = (ushort)((ReadByteAtAddress(operandHighAddress) << 8) | ReadByteAtAddress(operandLowAddress));
            var result = (ushort)~operand;

            sp = PreviousAddressForSP(operandLowAddress);
            WriteByteAtAddress(sp, (byte)(result & 0xFF));
            WriteByteAtAddress(NextAddressForSP(sp), (byte)((result & 0xFF00) >> 8));
            sp = NextAddressForSP(sp);
            sp = NextAddressForSP(sp);

            ip = NextAddressForIP(ip);
        }

        public void Compare()
        {
            var bHighAddress = PreviousAddressForSP(sp);
            var bLowAddress = PreviousAddressForSP(bHighAddress);
            var aHighAddress = PreviousAddressForSP(bLowAddress);
            var aLowAddress = PreviousAddressForSP(aHighAddress);

            var b = (ushort)((ReadByteAtAddress(bHighAddress) << 8) | ReadByteAtAddress(bLowAddress));
            var a = (ushort)((ReadByteAtAddress(aHighAddress) << 8) | ReadByteAtAddress(aLowAddress));
            
            flags &= 0b1111_1000;
            if (a == b)
            {
                flags = (byte)(flags | 1);
            }

            if (a > b)
            {
                flags = (byte)(flags | 2);
            }

            if (a < b)
            {
                flags = (byte)(flags | 4);
            }

            sp = PreviousAddressForSP(aLowAddress);
            ip = NextAddressForIP(ip);
        }

        public void JumpToAddress(JumpKind kind)
        {
            var addressLowAddress = NextAddressForIP(ip);
            var addressHighAddress = NextAddressForIP(addressLowAddress);
            var addressLow = ReadByteAtAddress(addressLowAddress);
            var addressHigh = ReadByteAtAddress(addressHighAddress);

            var shouldJump = kind switch
            {
                JumpKind.Equal => Equals,
                JumpKind.NotEqual => NotEquals,
                JumpKind.GreaterThan => GreaterThan,
                JumpKind.LessThan => LessThan,
                JumpKind.GreaterThanOrEqualTo => GreaterThanOrEqualTo,
                JumpKind.LessThanOrEqualTo => LessThanOrEqualTo,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            ip = shouldJump ? (ushort)((addressHigh << 8) | addressLow) : NextAddressForIP(addressHighAddress);
        }

        public void JumpToAddressInRegister(JumpKind kind)
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);

            var shouldJump = kind switch
            {
                JumpKind.Equal => Equals,
                JumpKind.NotEqual => NotEquals,
                JumpKind.GreaterThan => GreaterThan,
                JumpKind.LessThan => LessThan,
                JumpKind.GreaterThanOrEqualTo => GreaterThanOrEqualTo,
                JumpKind.LessThanOrEqualTo => LessThanOrEqualTo,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            ip = shouldJump ? address : NextAddressForIP(regNumAddress);
        }

        public void WriteFromAddress()
        {
            var addressLowAddress = NextAddressForIP(ip);
            var addressHighAddress = NextAddressForIP(addressLowAddress);

            var addressLow = ReadByteAtAddress(addressLowAddress);
            var addressHigh = ReadByteAtAddress(addressHighAddress);
            var address = (ushort)((addressHigh << 8) | addressLow);
            var output = new List<char>();

            while (ReadByteAtAddress(address) != 0x00)
            {
                output.Add((char)ReadByteAtAddress(address));
                address += 1;
            }

            ConsoleOutputWritten?.Invoke(this, new ConsoleOutputWrittenEventArgs
            {
                WrittenOutput = new string(output.ToArray())
            });

            ip = NextAddressForIP(addressHighAddress);
        }

        public void WriteFromAddressInRegister()
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);

            var output = new List<char>();

            while (ReadByteAtAddress(address) != 0x00)
            {
                output.Add((char)ReadByteAtAddress(address));
                address += 1;
            }

            ConsoleOutputWritten?.Invoke(this, new ConsoleOutputWrittenEventArgs
            {
                WrittenOutput = new string(output.ToArray())
            });

            ip = NextAddressForIP(regNumAddress);
        }
        
        public void ReadToAddress()
        {
            var addressLowAddress = NextAddressForIP(ip);
            var addressHighAddress = NextAddressForIP(addressLowAddress);
            var addressLow = ReadByteAtAddress(addressLowAddress);
            var addressHigh = ReadByteAtAddress(addressHighAddress);

            var address = (ushort)((addressHigh << 8) | addressLow);
            var input = getInput();

            foreach (var c in input)
            {
                WriteByteAtAddress(address, (byte)c);
                address += 1;
            }

            WriteByteAtAddress(address, 0x00);
            ip = NextAddressForIP(addressHighAddress);
        }

        public void ReadToAddressInRegister()
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            var input = getInput();

            foreach (var c in input)
            {
                WriteByteAtAddress(address, (byte)c);
                address += 1;
            }

            WriteByteAtAddress(address, 0x00);
            ip = NextAddressForIP(regNumAddress);
        }
        #endregion
    }
}
