using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Celarix.JustForFun.ForeverEx.Disassembly;
using Celarix.JustForFun.ForeverEx.Models;
using Celarix.JustForFun.ForeverExMemoryView;

namespace Celarix.JustForFun.ForeverEx
{
    internal sealed class Connector
    {
        private readonly ExecutionCore core;
        private readonly TerminalInterface terminal;

        private MainForm? memoryViewerForm;
        private readonly MemoryHistoryWatcher? memoryHistoryWatcher;

        private readonly byte[] memoryBuffer = new byte[TerminalInterface.MemoryViewerByteCount];
        private DisassembledInstruction[] disassemblyBuffer;
        private readonly byte[] disassemblyByteBuffer = new byte[128];

        private int runModeInstructionCount = 0;

        public Connector(ROMMappingMode mappingMode, string romImagePath, bool skipReads, string? dumpMemoryPath)
        {
            if (dumpMemoryPath != null)
            {
                memoryHistoryWatcher = new MemoryHistoryWatcher(Path.Combine(dumpMemoryPath, "memory.bin"));
            }

            core = new ExecutionCore(romImagePath, mappingMode, skipReads, memoryHistoryWatcher);
            terminal = new TerminalInterface();
            UpdateTerminal();

            core.ConsoleOutputWritten += Core_ConsoleOutputWritten;
            terminal.ConsoleInputEntered += Terminal_ConsoleInputEntered;
            terminal.PinAddressSet += Terminal_PinAddressSet;
        }

        public void Run()
        {
            while (MainLoop()) { }
        }

        private bool MainLoop()
        {
            if (terminal.RunningState == RunningState.Animating)
            {
                return AnimateLoop();
            }
            else if (terminal.RunningState == RunningState.Running)
            {
                return RunningLoop();
            }

            var key = Console.ReadKey(intercept: true);
            if (terminal.HandleKeyPressIfNeeded(key))
            {
                return true;
            }

            if (key.Key == ConsoleKey.F5)
            {
                terminal.RunningState = RunningState.Animating;
            }
            else if (key.Key == ConsoleKey.F10)
            {
                core.ExecuteSingleInstruction();
                UpdateTerminal();
                UpdateMemoryViewerPointers();

                if (core.WaitingForInput && core.LastConsoleInput == null)
                {
                    terminal.IsAcceptingUserInput = true;
                }
            }
            else if (key.Key == ConsoleKey.F2)
            {
                terminal.BeginPinMemory();
            }
            else if (key.Key == ConsoleKey.F3)
            {
                terminal.UnpinMemory();
            }
            else if (key.Key == ConsoleKey.F12)
            {
                if (memoryViewerForm != null)
                {
                    return true;
                }
                else
                {
                    OpenMemoryViewerForm();
                }
            }
            else if (key.Key == ConsoleKey.D && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                if (memoryHistoryWatcher != null)
                {
                    Console.Clear();
                    Console.SetCursorPosition(0, 0);

                    memoryHistoryWatcher.Dispose();
                    HistoryFrameBuilder.BuildFramesFromMemoryHistory(memoryHistoryWatcher.OutputFilePath, Path.GetDirectoryName(memoryHistoryWatcher.OutputFilePath)!);
                }

                return false;
            }

            return true;
        }

        private bool AnimateLoop()
        {
            core.ExecuteSingleInstruction();

            Thread.Sleep(100);

            if (core.WaitingForInput && core.LastConsoleInput == null)
            {
                terminal.RunningState = RunningState.Paused;
                terminal.IsAcceptingUserInput = true;
            }

            UpdateTerminal();
            UpdateMemoryViewerPointers();

            if (Console.KeyAvailable)
            {
                var animateKey = Console.ReadKey(intercept: true);
                if (animateKey.Key == ConsoleKey.F6)
                {
                    terminal.RunningState = RunningState.Paused;
                }
                else if (animateKey.Key == ConsoleKey.F7)
                {
                    core.ExecuteNOPSlide();
                    UpdateTerminal();
                    UpdateMemoryViewerPointers();
                }
                else if (animateKey.Key == ConsoleKey.F8)
                {
                    terminal.RunningState = RunningState.Running;
                    Console.Clear();
                    Console.SetCursorPosition(0, 0);
                    if (memoryViewerForm != null)
                    {
                        memoryViewerForm.WillWaitForRepaint = true;
                    }
                }
            }
            return true;
        }

        private bool RunningLoop()
        {
            core.ExecuteSingleInstruction();
            UpdateMemoryViewerPointers();
            runModeInstructionCount += 1;

            if (runModeInstructionCount % 100 == 0)
            {
                UpdateTerminal();

                if (Console.KeyAvailable)
                {
                    var runKey = Console.ReadKey(intercept: true);
                    if (runKey.Key == ConsoleKey.F6)
                    {
                        terminal.RunningState = RunningState.Paused;
                        if (memoryViewerForm != null)
                        {
                            memoryViewerForm.WillWaitForRepaint = false;
                        }
                        UpdateTerminal();
                        UpdateMemoryViewerPointers();
                        runModeInstructionCount = 0;
                    }
                }
            }

            return true;
        }

        private void Core_MemoryAddressChanged(object? sender, MemoryAddressChangedEventArgs e)
        {
            memoryViewerForm!.RedrawChangedMemory(e.AddressWithChange, e.NewValue);

            if (terminal.RunningState == RunningState.Running)
            {
                SharedSynchronization.WaitForRepaintComplete();
            }
        }

        private void Core_MemoryRangeChanged(object? sender, MemoryRangeChangedEventArgs e)
        {
            var buffer = new byte[e.ChangedRangeLength];
            core.FillBufferFromMemory(e.ChangedRangeStartAddress, buffer, e.ChangedRangeLength, 0);
            memoryViewerForm!.RedrawChangedMemoryRange(e.ChangedRangeStartAddress, buffer);

            if (terminal.RunningState == RunningState.Running)
            {
                SharedSynchronization.WaitForRepaintComplete();
            }
        }

        private void Core_ROMBankSwitched(object? sender, ROMBankSwitchEventArgs e)
        {
            var romBankBuffer = new byte[32768];
            core.FillBufferFromMemory(0x8000, romBankBuffer, romBankBuffer.Length, 0);

            memoryViewerForm!.ReplaceROMBank(romBankBuffer);

            if (terminal.RunningState == RunningState.Running)
            {
                SharedSynchronization.WaitForRepaintComplete();
            }
        }

        private void Terminal_PinAddressSet(object? sender, PinAddressSetEventArgs e)
        {
            
        }

        private void Terminal_ConsoleInputEntered(object? sender, ConsoleInputEventArgs e)
        {
            core.LastConsoleInput = e.EnteredInput;
        }

        private void Core_ConsoleOutputWritten(object? sender, ConsoleOutputWrittenEventArgs e)
        {
            terminal.WriteConsoleMessage(e.WrittenOutput);
            
            if (terminal.RunningState != RunningState.Running)
            {
                terminal.Draw();
            }
        }

        private void OpenMemoryViewerForm()
        {
            var ramBuffer = new byte[32768];
            core.FillBufferFromMemory(0, ramBuffer, ramBuffer.Length, 0);
            var romBankBuffer = new byte[32768];
            core.FillBufferFromMemory(0x8000, romBankBuffer, romBankBuffer.Length, 0);

            Task.Run(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                memoryViewerForm = new MainForm(ramBuffer, romBankBuffer, core.SP, core.IP);

                memoryViewerForm!.FormClosedByUser += (sender, args) =>
                {
                    core.MemoryViewerOpen = false;
                    core.MemoryAddressChanged -= Core_MemoryAddressChanged;
                    core.ROMBankSwitched -= Core_ROMBankSwitched;
                    core.MemoryRangeChanged -= Core_MemoryRangeChanged;
                    terminal.MemoryViewerOpen = false;
                    memoryViewerForm = null;
                };

                Application.Run(memoryViewerForm);
            });

            core.MemoryViewerOpen = true;
            core.MemoryAddressChanged += Core_MemoryAddressChanged;
            core.ROMBankSwitched += Core_ROMBankSwitched;
            core.MemoryRangeChanged += Core_MemoryRangeChanged;
            terminal.MemoryViewerOpen = true;
        }

        private void UpdateTerminal()
        {
            if (terminal.RunningState == RunningState.Running)
            {
                Console.WriteLine($"In Run Mode, executed {runModeInstructionCount} instructions. Press F6 to pause. SP: {core.SP}, IP: {core.IP}");
                return;
            }

            terminal.A = core.A;
            terminal.B = core.B;
            terminal.X = core.X;
            terminal.Y = core.Y;
            terminal.SP = core.SP;
            terminal.IP = core.IP;
            terminal.Flags = core.Flags;
            terminal.BankNum = core.Bank;

            core.FillBufferFromMemory(terminal.PinnedAddress.HasValue
                ? terminal.PinnedAddress.Value
                : terminal.MemoryViewerStartAddress,
                memoryBuffer,
                TerminalInterface.MemoryViewerByteCount,
                0);
            terminal.SetMemory(memoryBuffer);

            core.FillBufferFromMemory(core.IP, disassemblyByteBuffer, disassemblyByteBuffer.Length, 0);
            disassemblyBuffer = Disassembler
                .Disassemble(disassemblyByteBuffer, AssemblySource.RunningProgram, core.IP)
                .Take(TerminalInterface.DisassemblyLines)
                .ToArray();

            for (int i = 0; i < disassemblyBuffer.Length; i++)
            {
                if (core.IP == disassemblyBuffer[i].Address)
                {
                    disassemblyBuffer[i].IsCurrentInstruction = true;
                    break;
                }
            }

            terminal.SetDisassembly(disassemblyBuffer);

            terminal.Draw();
        }

        private void UpdateMemoryViewerPointers()
        {
            if (memoryViewerForm == null) { return; }

            memoryViewerForm.SetPointers(core.SP, core.IP);

            if (terminal.RunningState == RunningState.Running)
            {
                SharedSynchronization.WaitForRepaintComplete();
            }
        }
    }
}
