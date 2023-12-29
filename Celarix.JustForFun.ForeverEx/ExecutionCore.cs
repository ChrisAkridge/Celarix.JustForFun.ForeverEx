using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models;
using Celarix.JustForFun.ForeverEx.Models.MemoryHistory;

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

        public const int InstructionCount = 36;
        private const int RandomSeed = 0x12345678;
        private const int ExecutedConsecutiveNOPsToTriggerNOPSlideExectuor = 16;

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

        private new bool Equals => (flags & 7) == 1;
        private bool LessThan => (flags & 7) == 4;
        private bool GreaterThan => (flags & 7) == 2;
        private bool LessThanOrEqualTo => (flags & 7) == 5;
        private bool GreaterThanOrEqualTo => (flags & 7) == 3;
        private bool NotEquals => (flags & 7) == 0;
        #endregion

        public event EventHandler<ROMBankSwitchEventArgs> ROMBankSwitched;
        public event EventHandler<MemoryAddressChangedEventArgs> MemoryAddressChanged;
        public event EventHandler<MemoryRangeChangedEventArgs> MemoryRangeChanged;
        public event EventHandler<ConsoleOutputWrittenEventArgs> ConsoleOutputWritten;

        private bool waitingForInput;
        private ushort? inputDestinationAddress;
        private readonly bool skipReads;
        private readonly Random random = new Random(RandomSeed);
        private int consecutiveNOPsExecuted = 0;

        public bool MemoryViewerOpen { get; set; }
        public string? LastConsoleInput { get; set; }

        public MemoryHistoryWatcher? MemoryHistoryWatcher { get; set; }

        #region Display Properties
        public ushort A => a;
        public ushort B => b;
        public ushort X => x;
        public ushort Y => y;
        public ushort SP => sp;
        public ushort IP => ip;
        public byte Bank => BankNum;
        public byte Flags => flags;

        public bool WaitingForInput { get => waitingForInput; private set => waitingForInput = value; }
        #endregion

        public ExecutionCore(string romImagePath,
            ROMMappingMode romMappingMode,
            bool skipReads,
            MemoryHistoryWatcher? memoryHistoryWatcher)
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

            MemoryHistoryWatcher = memoryHistoryWatcher;
            SwitchBank(0);
            ip = 0x8000;
            this.skipReads = skipReads;
        }

        public void ExecuteSingleInstruction()
        {
            if (WaitingForInput)
            {
                CheckForInput();
            }

            var opcode = ReadByteAtAddress(ip) % InstructionCount;
            if (opcode == 0)
            {
                NoOperation();
            }
            else if (opcode == 1)
            {
                LoadImmediate();
            }
            else if (opcode == 2)
            {
                MoveRegisterValue();
            }
            else if (opcode == 3)
            {
                LoadA();
            }
            else if (opcode == 4)
            {
                LoadB();
            }
            else if (opcode == 5)
            {
                StoreA();
            }
            else if (opcode == 6)
            {
                StoreB();
            }
            else if (opcode == 7)
            {
                PushToStack();
            }
            else if (opcode == 8)
            {
                PopFromStack();
            }
            else if (opcode is >= 9 and <= 15)
            {
                MathOperation(opcode switch
                {
                    9 => MathOperationKind.Add,
                    10 => MathOperationKind.Subtract,
                    11 => MathOperationKind.Multiply,
                    12 => MathOperationKind.Divide,
                    13 => MathOperationKind.BitwiseAnd,
                    14 => MathOperationKind.BitwiseOr,
                    15 => MathOperationKind.BitwiseXor,
                    _ => throw new InvalidOperationException("This should never happen.")
                });
            }
            else if (opcode == 16)
            {
                BitwiseNOT();
            }
            else if (opcode == 17)
            {
                Compare();
            }
            else if (opcode is >= 18 and <= 23)
            {
                JumpToAddress(opcode switch
                {
                    18 => JumpKind.Equal,
                    19 => JumpKind.NotEqual,
                    20 => JumpKind.LessThan,
                    21 => JumpKind.GreaterThan,
                    22 => JumpKind.LessThanOrEqualTo,
                    23 => JumpKind.GreaterThanOrEqualTo,
                    _ => throw new InvalidOperationException("This should never happen.")
                });
            }
            else if (opcode is >= 24 and <= 29)
            {
                JumpToAddressInRegister(opcode switch
                {
                    24 => JumpKind.Equal,
                    25 => JumpKind.NotEqual,
                    26 => JumpKind.LessThan,
                    27 => JumpKind.GreaterThan,
                    28 => JumpKind.LessThanOrEqualTo,
                    29 => JumpKind.GreaterThanOrEqualTo,
                    _ => throw new InvalidOperationException("This should never happen.")
                });
            }
            else if (opcode == 30)
            {
                WriteFromAddress();
            }
            else if (opcode == 31)
            {
                WriteFromAddressInRegister();
            }
            else if (opcode == 32)
            {
                ReadToAddress();
            }
            else if (opcode == 33)
            {
                ReadToAddressInRegister();
            }
            else if (opcode == 34)
            {
                AddToRegister();
            }
            else if (opcode == 35)
            {
                SubtractFromRegister();
            }
            else
            {
                throw new InvalidOperationException("This should never happen.");
            }
        }

        private void CheckForInput()
        {
            if (LastConsoleInput == null)
            {
                throw new InvalidOperationException("No console input was provided.");
            }

            if (!inputDestinationAddress.HasValue)
            {
                throw new InvalidOperationException("No input destination address was provided.");
            }

            var currentAddress = inputDestinationAddress.Value;
            var bytesToWrite = Encoding.ASCII.GetBytes(LastConsoleInput);
            foreach (var c in bytesToWrite)
            {
                WriteByteAtAddress(currentAddress, (byte)c, suppressEvent: true);
                currentAddress++;
            }

            WriteByteAtAddress(currentAddress, 0, suppressEvent: true);

            var writtenLength = currentAddress - inputDestinationAddress.Value;
            MemoryRangeChanged?.Invoke(this, new MemoryRangeChangedEventArgs(inputDestinationAddress.Value, writtenLength));
            MemoryHistoryWatcher?.WriteEvent(new MemoryRangeChanged(inputDestinationAddress.Value, bytesToWrite));

            LastConsoleInput = null;
            inputDestinationAddress = null;
            WaitingForInput = false;
        }

        public void ExecuteNOPSlide()
        {
            while (ReadByteAtAddress(ip) == 0)
            {
                ExecuteSingleInstruction();
            }
        }

        // TODO: fix disassembler
        // TODO: add constant run mode with ManualResetEvent
        // TODO: lift the image drawing code into its own class and add a mode that has it output memory updates as PNGs
        private void SwitchBank(int bankNumber)
        {
            bankNumber &= 0xF;

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

            if (MemoryHistoryWatcher != null)
            {
                var eventBuffer = new byte[ROMBankSize];
                FillBufferFromMemory(0x8000, eventBuffer, 0x8000, 0);
                MemoryHistoryWatcher.WriteEvent(new ROMBankSwitched(eventBuffer));
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
                int bytesRead = romImage.Read(currentROMBank, 0, ROMBankSize);
                if (bytesRead == 0)
                {
                    SwitchBank(0);
                }
            }

            if (MemoryViewerOpen)
            {
                ROMBankSwitched?.Invoke(this, new ROMBankSwitchEventArgs());
            }
        }

        private byte ReadByteAtAddress(ushort address) =>
            address < 0x8000 ? ram[address] : currentROMBank[address - 0x8000];

        private void WriteByteAtAddress(ushort address,
            byte value,
            bool suppressEvent = false)
        {
            if (address >= 0x8000) { return; }

            var oldValue = ram[address];
            ram[address] = value;

            if (MemoryViewerOpen && oldValue != value && !suppressEvent)
            {
                MemoryAddressChanged?.Invoke(this, new MemoryAddressChangedEventArgs(address, value));
                MemoryHistoryWatcher?.WriteEvent(new SingleByteChanged(address, value));
            }
        }

        private void SetRegister(int registerNumber, ushort value)
        {
            registerNumber &= 0b111;

            if (registerNumber == 0) { a = value; }
            else if (registerNumber == 1) { b = value; }
            else if (registerNumber == 2) { x = value; }
            else if (registerNumber == 3) { y = value; }
            else if (registerNumber == 4) { sp = value; }
            else if (registerNumber == 5)
            {
                if (random.NextDouble() < 0.99d)
                {
                    // Helps us from getting stuck in a loop
                    // Should be kinda deterministic because we seed the RNG with a constant
                    // Also this makes this architecture non-Turing-complete because no program ever halts
                    ip = value;
                }
            }
            else if (registerNumber == 6) { BankNum = (byte)value; }
            else if (registerNumber == 7) { flags = (byte)value; }
            else { throw new ArgumentOutOfRangeException(nameof(registerNumber)); }
        }

        public ushort GetRegister(int registerNumber) =>
            (registerNumber & 0b111) switch
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

        private static ushort NextAddressForSP(ushort address) =>
            // SP is restricted to 0x0000 to 0x7FFF
            address == 0x7FFF ? (ushort)0x0000 : (ushort)(address + 1);

        private static ushort PreviousAddressForSP(ushort address) =>
            // SP is restricted to 0x0000 to 0x7FFF
            address == 0x0000 ? (ushort)0x7FFF : (ushort)(address - 1);

        private ushort NextAddressForIP(ushort address)
        {
            // When incrementing IP, it wraps around to 0x8000 when it reaches 0xFFFF
            // but it just increments past 0x7FFF. This prevents execution from getting
            // stuck in a blank or mostly-blank RAM.
            if (address == 0xFFFF)
            {
                if (romMappingMode == ROMMappingMode.OverflowShifting)
                {
                    NextBank();
                    return 0x8000;
                }
            }
            return (ushort)(address + 1);
        }

        public void FillBufferFromMemory(ushort address, byte[] buffer, int count, int start)
        {
            for (int i = 0; i < count; i++)
            {
                var sourceIndex = address + i;
                var destinationIndex = start + i;
                if (destinationIndex >= buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(start));
                }

                buffer[destinationIndex] = sourceIndex <= 0x7FFF
                    ? ram[sourceIndex]
                    : currentROMBank[sourceIndex - 0x8000];
            }
        }

        #region Instructions
        public void NoOperation()
        {
            ip = NextAddressForIP(ip);
            consecutiveNOPsExecuted += 1;

            if (consecutiveNOPsExecuted == ExecutedConsecutiveNOPsToTriggerNOPSlideExectuor)
            {
                ExecuteNOPSlide();
                consecutiveNOPsExecuted = 0;
            }
        }

        public void LoadImmediate()
        {
            ushort registerNumberAddress = NextAddressForIP(ip);
            var registerNumber = ReadByteAtAddress(registerNumberAddress);

            ushort valueLowAddress = NextAddressForIP(registerNumberAddress);
            var valueLow = ReadByteAtAddress(valueLowAddress);

            ushort valueHighAddress = NextAddressForIP(valueLowAddress);
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
            sp = NextAddressForSP(NextAddressForSP(sp));
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

            shouldJump = random.NextDouble() >= 0.99d && shouldJump;

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

            shouldJump = random.NextDouble() >= 0.99d && shouldJump;

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
            ip = NextAddressForIP(addressHighAddress);

            WaitingForInput = true;
            inputDestinationAddress = address;

            if (skipReads)
            {
                // Allow the program to continue without waiting for input
                LastConsoleInput = random.NextDouble() > 0.5
                    ? "My hovercraft is full of eels"
                    : "There's a horse in aisle five";
            }
        }

        public void ReadToAddressInRegister()
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var address = GetRegister(regNum);
            ip = NextAddressForIP(regNumAddress);

            WaitingForInput = true;
            inputDestinationAddress = address;

            if (skipReads)
            {
                // Allow the program to continue without waiting for input
                LastConsoleInput = random.NextDouble() > 0.5
                    ? "My hovercraft is full of eels"
                    : "There's a horse in aisle five";
            }
        }

        public void AddToRegister()
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var reg = GetRegister(regNum);
            var operandHighAddress = NextAddressForIP(regNumAddress);
            var operandLowAddress = NextAddressForIP(operandHighAddress);
            var operand = (ushort)((ReadByteAtAddress(operandHighAddress) << 8) | ReadByteAtAddress(operandLowAddress));
            var result = (ushort)(reg + operand);
            SetRegister(regNum, result);
            ip = NextAddressForIP(operandLowAddress);
        }

        public void SubtractFromRegister()
        {
            var regNumAddress = NextAddressForIP(ip);
            var regNum = ReadByteAtAddress(regNumAddress);
            var reg = GetRegister(regNum);
            var operandHighAddress = NextAddressForIP(regNumAddress);
            var operandLowAddress = NextAddressForIP(operandHighAddress);
            var operand = (ushort)((ReadByteAtAddress(operandHighAddress) << 8) | ReadByteAtAddress(operandLowAddress));
            var result = (ushort)(reg - operand);
            SetRegister(regNum, result);
            ip = NextAddressForIP(operandLowAddress);
        }
        #endregion
    }
}
