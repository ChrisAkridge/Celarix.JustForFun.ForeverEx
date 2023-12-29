using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models;

namespace Celarix.JustForFun.ForeverEx
{
    internal sealed class TerminalInterface
    {
        private const int TerminalWidth = 86;
        private const int TerminalHeight = 33;
        public const int MemoryViewerByteCount = 80;
        private const int MemoryViewerByteCountPerLine = 8;
        private const int BytesBeforeSPForDisplay = 24;
        public const int DisassemblyLines = 10;
        private const int ConsoleLineLength = 60;
        private const int ConsoleOutputLines = 10;

        private const char VerticalBorder = '│';

        #region Screen Element Dimensions
        private const int ProgramX = 0;
        private const int ProgramY = 0;
        private const int ProgramWidth = 40;
        private const int ProgramHeight = 14;

        private const int MemoryX = 41;
        private const int MemoryY = 0;
        private const int MemoryWidth = 44;
        private const int MemoryHeight = 14;

        private const int RegistersX = 0;
        private const int RegistersY = 15;
        private const int RegistersWidth = 19;
        private const int RegistersHeight = 8;

        private const int PinMemoryX = 0;
        private const int PinMemoryY = 24;
        private const int PinMemoryWidth = 17;
        private const int PinMemoryHeight = 7;

        private const int ConsoleX = 20;
        private const int ConsoleY = 15;
        private const int ConsoleWidth = 64;
        private const int ConsoleHeight = 16;

        private const int CommandRowX = 0;
        private const int CommandRowY = 32;
        #endregion

        private ushort a;
        private ushort b;
        private ushort x;
        private ushort y;
        private ushort sp;
        private ushort ip;
        private ushort bank;
        private ushort flags;

        private ConsoleColor initialColor;
        private byte[] memoryBuffer = new byte[MemoryViewerByteCount];
        private ushort memoryViewerStartAddress = 0;
        private DisassembledInstruction[] disassemblyBuffer = new DisassembledInstruction[DisassemblyLines];

        private Queue<string> consoleOutputHistory = new Queue<string>(ConsoleOutputLines);
        private char[] inputBuffer = new char[ConsoleLineLength];
        private int inputBufferEditorChar = 0;
        private int inputLength = 0;

        private PinningMemoryState pinningMemoryState = PinningMemoryState.NotPinned;
        private ushort? pinnedAddress = null;
        private int pinnedAddressEditorChar = 0;

        public event EventHandler<PinAddressSetEventArgs> PinAddressSet;
        public event EventHandler<ConsoleInputEventArgs> ConsoleInputEntered;

        #region Register Properties
        public ushort A
        {
            get => a;
            set
            {
                a = value;
                DrawRegisters();
            }
        }

        public ushort B
        {
            get => b;
            set
            {
                b = value;
                DrawRegisters();
            }
        }

        public ushort X
        {
            get => x;
            set
            {
                x = value;
                DrawRegisters();
            }
        }

        public ushort Y
        {
            get => y;
            set
            {
                y = value;
                DrawRegisters();
            }
        }

        public ushort SP
        {
            get => sp;
            set
            {
                sp = value;
                DrawRegisters();

                var rowAlignedSP = sp - (sp % MemoryViewerByteCountPerLine);
                if (rowAlignedSP <= BytesBeforeSPForDisplay)
                {
                    memoryViewerStartAddress = 0;
                }
                else if (rowAlignedSP > BytesBeforeSPForDisplay)
                {
                    memoryViewerStartAddress = (ushort)(rowAlignedSP - BytesBeforeSPForDisplay);
                }
                DrawMemoryViewer();
            }
        }

        public ushort IP
        {
            get => ip;
            set
            {
                ip = value;
                DrawProgramViewer();
            }
        }

        public ushort BankNum
        {
            get => bank;
            set
            {
                bank = value;
                DrawProgramViewer();
            }
        }

        public ushort Flags
        {
            get => flags;
            set
            {
                flags = value;
                DrawRegisters();
            }
        }
        #endregion

        public ushort? PinnedAddress => pinnedAddress;
        public ushort MemoryViewerStartAddress => memoryViewerStartAddress;

        public RunningState RunningState { get; set; }
        public bool IsAcceptingUserInput { get; set; }
        public bool IsAwaitingKeyPress => pinningMemoryState == PinningMemoryState.WaitingForAddress || IsAcceptingUserInput;
        public bool MemoryViewerOpen { get; set; }

        public TerminalInterface()
        {
            Array.Fill(inputBuffer, ' ');

            initialColor = Console.BackgroundColor;
            Console.SetWindowSize(TerminalWidth, TerminalHeight);
            Console.SetBufferSize(TerminalWidth, TerminalHeight);
            Console.CursorVisible = false;

            Draw();
        }

        public bool HandleKeyPressIfNeeded(ConsoleKeyInfo key)
        {
            if (pinningMemoryState == PinningMemoryState.WaitingForAddress)
            {
                OnPinnedMemoryKeyPress(key);
                return true;
            }
            else if (IsAcceptingUserInput)
            {
                OnInputKeyPress(key);
                return true;
            }

            return false;
        }

        public void BeginPinMemory()
        {
            pinningMemoryState = PinningMemoryState.WaitingForAddress;
            DrawPinMemory();
        }

        public void UnpinMemory()
        {
            pinningMemoryState = PinningMemoryState.NotPinned;
            pinnedAddress = null;
            DrawPinMemory();
        }

        private void OnInputKeyPress(ConsoleKeyInfo key)
        {
            // Left, right: Move the cursor in the input buffer
            if (key.Key == ConsoleKey.LeftArrow && inputBufferEditorChar > 0)
            {
                inputBufferEditorChar--;
                DrawConsole();
            }
            else if (key.Key == ConsoleKey.RightArrow && inputBufferEditorChar < inputLength - 1)
            {
                inputBufferEditorChar++;
                DrawConsole();
            }
            // Backspace: Delete the last character in the buffer and move the cursor back
            else if (key.Key == ConsoleKey.Backspace && inputBufferEditorChar > 0)
            {
                inputBuffer[inputLength] = ' ';
                inputBufferEditorChar--;
                inputLength--;
                DrawConsole();
            }
            // Delete: Delete the next character in the buffer, if any
            else if (key.Key == ConsoleKey.Delete
                && inputBufferEditorChar < inputLength - 1
                && inputLength > 0)
            {
                // Cursor position: where we're at now
                // Cursor position + 1: the character we want to delete
                // Cursor position + 2: the start of the characters we want to move back
                for (int i = inputBufferEditorChar + 2; i < inputLength; i++)
                {
                    inputBuffer[i - 1] = inputBuffer[i];
                }

                // Write a space in the last character since we shifted everything back
                inputBuffer[inputLength - 1] = ' ';
            }
            // Enter: Submit the input, clear the buffer, and disable input editing
            else if (key.Key == ConsoleKey.Enter)
            {
                inputBufferEditorChar = 0;
                var enteredInput = new string(inputBuffer, 0, inputLength);
                Console.CursorVisible = false;
                IsAcceptingUserInput = false;
                ConsoleInputEntered?.Invoke(this, new ConsoleInputEventArgs
                {
                    EnteredInput = enteredInput
                });
                DrawConsole();
            }
            // Anything else that is a character: add it to the buffer unless we're out of room
            else if (key.KeyChar is >= ' ' and <= '~' && inputBufferEditorChar < ConsoleLineLength - 1)
            {
                // Cursor position: where we're at now
                // Cursor position + 1: the start of the characters we want to move forward
                // Cursor position + 2: the end of the characters we want to move forward
                for (int i = inputLength; i > inputBufferEditorChar; i--)
                {
                    inputBuffer[i] = inputBuffer[i - 1];
                }

                inputBuffer[inputBufferEditorChar] = key.KeyChar;
                inputBufferEditorChar++;
                inputLength++;
                DrawConsole();
            }
        }

        private void OnPinnedMemoryKeyPress(ConsoleKeyInfo key)
        {
            if (pinnedAddress == null)
            {
                throw new InvalidOperationException("Set pinned address before entering pinning mode.");
            }

            // 0-9, A-F: Write the character to the address editor and advance the cursor if possible
            if ((key.KeyChar is >= '0' and <= '9')
                || (key.KeyChar is >= 'A' and <= 'F')
                || (key.KeyChar is >= 'a' and <= 'f'))
            {
                int nybble = key.KeyChar switch
                {
                    >= '0' and <= '9' => key.KeyChar - '0',
                    >= 'A' and <= 'F' => key.KeyChar - 'A' + 10,
                    >= 'a' and <= 'f' => key.KeyChar - 'a' + 10,
                    _ => throw new InvalidOperationException("Unreachable.")
                };
                int shiftAmount = pinnedAddressEditorChar switch
                {
                    0 => 12,
                    1 => 8,
                    2 => 4,
                    3 => 0,
                    _ => throw new InvalidOperationException("Unreachable.")
                };
                ushort shiftedNybble = (ushort)(nybble << shiftAmount);
                ushort mask = (ushort)~(0xF << shiftAmount);
                pinnedAddress = (ushort)((pinnedAddress & mask) | shiftedNybble);

                if (pinnedAddressEditorChar < 3)
                {
                    pinnedAddressEditorChar += 1;
                }

                DrawPinMemory();
            }
            // Left, right: Move the cursor
            else if (key.Key == ConsoleKey.LeftArrow)
            {
                if (pinnedAddressEditorChar > 0)
                {
                    pinnedAddressEditorChar -= 1;
                    DrawPinMemory();
                }
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                if (pinnedAddressEditorChar < 3)
                {
                    pinnedAddressEditorChar += 1;
                    DrawPinMemory();
                }
            }
            // Backspace: Delete the last character and move backward
            else if (key.Key == ConsoleKey.Backspace)
            {
                int shiftAmount = pinnedAddressEditorChar switch
                {
                    0 => 12,
                    1 => 8,
                    2 => 4,
                    3 => 0,
                    _ => throw new InvalidOperationException("Unreachable.")
                };
                ushort mask = (ushort)~(0xF << shiftAmount);
                pinnedAddress = (ushort)(pinnedAddress & mask);
                DrawPinMemory();

                if (pinnedAddressEditorChar > 0)
                {
                    pinnedAddressEditorChar -= 1;
                }
            }
            // Enter: Accept the address and pin the memory
            else if (key.Key == ConsoleKey.Enter)
            {
                pinningMemoryState = PinningMemoryState.Pinned;
                PinAddressSet?.Invoke(this, new PinAddressSetEventArgs
                {
                    PinnedAddress = pinnedAddress.Value
                });
                Console.CursorVisible = false;
            }
            // Esc: Disable address pinning
            else if (key.Key == ConsoleKey.Escape)
            {
                pinningMemoryState = PinningMemoryState.NotPinned;
                pinnedAddress = null;
                pinnedAddressEditorChar = 0;
                DrawPinMemory();
                Console.CursorVisible = false;
            }
        }

        public void SetMemory(byte[] memory)
        {
            if (memory.Length != MemoryViewerByteCount)
            {
                throw new ArgumentException($"Memory must be {MemoryViewerByteCount} bytes long.");
            }

            Array.Copy(memory, memoryBuffer, MemoryViewerByteCount);

            DrawMemoryViewer();
        }

        public void SetDisassembly(DisassembledInstruction[] disassembly)
        {
            if (disassembly.Length > DisassemblyLines)
            {
                throw new ArgumentException($"Disassembly must be no more than {DisassemblyLines} lines long.");
            }

            Array.Clear(disassemblyBuffer, 0, disassemblyBuffer.Length);
            Array.Copy(disassembly, disassemblyBuffer, disassembly.Length);

            DrawProgramViewer();
        }

        public void WriteConsoleMessage(string message)
        {
            var splitLines = new List<string>();
            while (message.Length > ConsoleLineLength)
            {
                splitLines.Add(message.Substring(0, ConsoleLineLength));
                message = message.Substring(ConsoleLineLength);
            }

            foreach (var line in splitLines)
            {
                WriteConsoleLine(line);
            }
        }

        private void WriteConsoleLine(string line)
        {
            if (consoleOutputHistory.Count == ConsoleOutputLines)
            {
                consoleOutputHistory.Dequeue();
            }

            consoleOutputHistory.Enqueue(line);
        }

        #region Drawing
        public void Draw()
        {
            Console.Clear();
            DrawProgramViewer();
            DrawMemoryViewer();
            DrawRegisters();
            DrawConsole();
            DrawPinMemory();
            DrawCommands();
        }

        private void DrawTopBorderLine(int x, int y, int width)
        {
            Console.SetCursorPosition(x, y);
            Console.Write("┌");
            Console.Write(new string('─', width - 2));
            Console.Write("┐");
        }

        private void DrawBottomBorderLine(int x, int y, int width)
        {
            Console.SetCursorPosition(x, y);
            Console.Write("└");
            Console.Write(new string('─', width - 2));
            Console.Write("┘");
        }

        private void DrawInnerBorderLine(int x, int y, int width)
        {
            Console.SetCursorPosition(x, y);
            Console.Write("├");
            Console.Write(new string('─', width - 2));
            Console.Write("┤");
        }

        private void DrawTitleBar(int x, int y, int width, string title)
        {
            DrawTopBorderLine(x, y, width);
            Console.SetCursorPosition(x, y + 1);
            Console.Write(VerticalBorder);

            var totalTitleLength = width - 2;
            var leftPadding = (totalTitleLength - title.Length) / 2;
            var rightPadding = totalTitleLength - title.Length - leftPadding;
            var totalTitle = new string(' ', leftPadding) + title + new string(' ', rightPadding);

            Console.Write(totalTitle);
            Console.Write(VerticalBorder);
            DrawInnerBorderLine(x, y + 2, width);
        }

        private void DrawProgramViewer()
        {
            DrawTitleBar(ProgramX, ProgramY, ProgramWidth, "PROGRAM");

            for (int i = 0; i < disassemblyBuffer.Length; i++)
            {
                Console.SetCursorPosition(ProgramX, ProgramY + 3 + i);
                Console.Write(VerticalBorder);

                if (disassemblyBuffer[i] != null)
                {
                    string disassembledInstruction = GetInstructionString(disassemblyBuffer[i]);
                    if (disassembledInstruction.Length < ProgramWidth - 2)
                    {
                        disassembledInstruction += new string(' ', ProgramWidth - 2 - disassembledInstruction.Length);
                    }

                    Console.Write(disassembledInstruction);
                }
                else
                {
                    Console.Write(new string(' ', ProgramWidth - 2));
                }

                Console.Write(VerticalBorder);
            }

            DrawBottomBorderLine(ProgramX, ProgramY + ProgramHeight - 1, ProgramWidth);
        }

        private string GetInstructionString(DisassembledInstruction instruction)
        {
            var currentInstructionString = instruction.IsCurrentInstruction ? ">" : " ";
            var addressString = instruction.Address.ToString("X4");
            var opcodeString = instruction.Opcode.ToString("X2");
            var operandByte1String = instruction.OperandByte1.HasValue ? instruction.OperandByte1.Value.ToString("X2") : "  ";
            var operandByte2String = instruction.OperandByte2.HasValue ? instruction.OperandByte2.Value.ToString("X2") : "  ";
            var operandByte3String = instruction.OperandByte3.HasValue ? instruction.OperandByte3.Value.ToString("X2") : "  ";

            return $"{currentInstructionString} {addressString} {opcodeString} {operandByte1String} {operandByte2String} {operandByte3String} {instruction.Mnemonic}";
        }

        private void DrawMemoryViewer()
        {
            DrawTitleBar(MemoryX, MemoryY, MemoryWidth, "MEMORY");

            var currentSystemAddress = memoryViewerStartAddress;
            var currentBufferAddress = 0;

            for (int row = 0; row < (MemoryViewerByteCount / MemoryViewerByteCountPerLine); row++)
            {
                Console.SetCursorPosition(MemoryX, MemoryY + 3 + row);
                Console.Write(VerticalBorder + " ");
                Console.Write(currentSystemAddress.ToString("X4") + "  ");

                for (int column = 0; column < MemoryViewerByteCountPerLine; column++)
                {
                    if (sp == currentSystemAddress || sp + 1 == currentSystemAddress)
                    {
                        Console.BackgroundColor = ConsoleColor.Blue;
                        Console.Write(memoryBuffer[currentBufferAddress].ToString("X2"));
                        Console.BackgroundColor = initialColor;
                    }
                    else
                    {
                        Console.Write(memoryBuffer[currentBufferAddress].ToString("X2"));
                    }

                    Console.Write(" ");
                    currentSystemAddress += 1;
                    currentBufferAddress += 1;
                }

                Console.Write("  ");
                currentSystemAddress -= MemoryViewerByteCountPerLine;
                currentBufferAddress -= MemoryViewerByteCountPerLine;
                for (int asciiColumn = 0; asciiColumn < MemoryViewerByteCountPerLine; asciiColumn++)
                {
                    var currentByte = memoryBuffer[currentBufferAddress];
                    var currentChar = currentByte >= 32 && currentByte <= 126 ? (char)currentByte : '.';
                    if (sp == currentSystemAddress || sp + 1 == currentSystemAddress)
                    {
                        Console.BackgroundColor = ConsoleColor.Blue;
                        Console.Write(currentChar);
                        Console.BackgroundColor = initialColor;
                    }
                    else
                    {
                        Console.Write(currentChar);
                    }

                    currentSystemAddress += 1;
                    currentBufferAddress += 1;
                }

                Console.Write(" " + VerticalBorder);
            }

            DrawBottomBorderLine(MemoryX, MemoryY + MemoryHeight - 1, MemoryWidth);
        }

        private void DrawRegisters()
        {
            DrawTitleBar(RegistersX, RegistersY, RegistersWidth, "REGISTERS");

            Console.SetCursorPosition(RegistersX, RegistersY + 3);
            Console.Write($"{VerticalBorder}  A {a:X4}  B {b:X4} {VerticalBorder}");
            Console.SetCursorPosition(RegistersX, RegistersY + 4);
            Console.Write($"{VerticalBorder}  X {x:X4}  Y {y:X4} {VerticalBorder}");
            Console.SetCursorPosition(RegistersX, RegistersY + 5);
            Console.Write($"{VerticalBorder} SP {sp:X4} IP {ip:X4} {VerticalBorder}");
            Console.SetCursorPosition(RegistersX, RegistersY + 6);
            Console.Write($"{VerticalBorder} BANK {bank:X1}  FLAG {flags:X2} {VerticalBorder}");

            DrawBottomBorderLine(RegistersX, RegistersY + RegistersHeight - 1, RegistersWidth);
        }

        private void DrawConsole()
        {
            var oldCursorLeft = Console.CursorLeft;
            var oldCursorTop = Console.CursorTop;

            DrawTitleBar(ConsoleX, ConsoleY, ConsoleWidth, "CONSOLE");
            var blankLines = ConsoleOutputLines - consoleOutputHistory.Count;
            for (var i = 0; i < ConsoleOutputLines; i++)
            {
                Console.SetCursorPosition(ConsoleX, ConsoleY + 3 + i);
                if (i < blankLines)
                {
                    Console.Write(VerticalBorder + new string(' ', ConsoleWidth - 2) + VerticalBorder);
                }
                else
                {
                    Console.Write(VerticalBorder + " ");
                    var currentLine = consoleOutputHistory.ElementAt(i);

                    if (currentLine == null)
                    {
                        Console.Write(new string(' ', ConsoleLineLength));
                    }
                    else
                    {
                        if (currentLine.Length < ConsoleLineLength)
                        {
                            currentLine += new string(' ', ConsoleLineLength - currentLine.Length);
                        }

                        foreach (char c in currentLine)
                        {
                            if (c >= ' ' && c <= '~')
                            {
                                Console.Write(c);
                            }
                            else
                            {
                                Console.Write('.');
                            }
                        }
                    }

                    Console.Write(" " + VerticalBorder);
                }
            }

            DrawInnerBorderLine(ConsoleX, ConsoleY + ConsoleOutputLines + 1, ConsoleWidth);
            Console.SetCursorPosition(ConsoleX, ConsoleY + ConsoleOutputLines + 2);
            Console.Write(VerticalBorder + " ");
            if (IsAcceptingUserInput)
            {
                Console.Write(new string(inputBuffer) + new string(' ', ConsoleLineLength - inputBuffer.Length));
            }
            else
            {
                Console.Write(new string(' ', ConsoleLineLength));
            }
            Console.WriteLine(" " + VerticalBorder);
            DrawBottomBorderLine(ConsoleX, ConsoleY + ConsoleHeight - 3, ConsoleWidth);

            Console.SetCursorPosition(oldCursorLeft, oldCursorTop);
        }

        private void DrawPinMemory()
        {
            if (pinningMemoryState == PinningMemoryState.NotPinned)
            {
                return;
            }

            if (pinnedAddress == null)
            {
                throw new Exception("Pinned address is null when pinning; initialize it to 0");
            }

            var oldCursorLeft = Console.CursorLeft;
            var oldCursorTop = Console.CursorTop;

            DrawTitleBar(PinMemoryX, PinMemoryY, PinMemoryWidth, "PIN MEMORY");
            Console.SetCursorPosition(PinMemoryX, PinMemoryY + 3);
            Console.Write(VerticalBorder + new string(' ', PinMemoryWidth - 2) + VerticalBorder);
            Console.SetCursorPosition(PinMemoryX, PinMemoryY + 4);
            Console.Write(VerticalBorder + " Address ");

            Console.BackgroundColor = pinningMemoryState == PinningMemoryState.WaitingForAddress
                ? ConsoleColor.Cyan
                : ConsoleColor.Green;
            Console.Write(pinnedAddress.Value.ToString("X4"));
            Console.BackgroundColor = initialColor;
            Console.Write(" " + VerticalBorder);


            Console.SetCursorPosition(PinMemoryX, PinMemoryY + 5);
            Console.Write(VerticalBorder + new string(' ', PinMemoryWidth - 2) + VerticalBorder);
            DrawBottomBorderLine(PinMemoryX, PinMemoryY + PinMemoryHeight - 1, PinMemoryWidth);

            Console.SetCursorPosition(oldCursorLeft, oldCursorTop);
        }

        private void DrawCommands()
        {
            Console.SetCursorPosition(CommandRowX, CommandRowY);

            Dictionary<string, string>? commands;
            if (RunningState == RunningState.Paused)
            {
                commands = new Dictionary<string, string>
                {
                    { "F5", "Animate" },
                    { "F10", "Step" },
                    { "F2", "Pin Memory" },
                    { "F3", "Unpin" },
                    { "F12", "Memview" },
                    { "^D", "Exit" }
                };
            }
            else
            {
                commands = new Dictionary<string, string>
                {
                    { "F6", "Pause" },
                    { "F7", "Execute NOP Slide" },
                    { "F8", "Run" }
                };
            }

            foreach (var command in commands)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write(command.Key);
                Console.BackgroundColor = initialColor;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" " + command.Value + " ");
            }
        }
        #endregion
    }
}
