using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Celarix.JustForFun.ForeverEx.Models;
using Celarix.JustForFun.ForeverExMemoryView;

namespace Celarix.JustForFun.ForeverEx
{
    internal sealed class Connector
    {
        private readonly ExecutionCore core;
        private readonly TerminalInterface terminal;

        private MainForm? memoryViewerForm;

        private readonly byte[] memoryBuffer = new byte[TerminalInterface.MemoryViewerByteCount];
        private readonly DisassembledInstruction[] disassemblyBuffer = new DisassembledInstruction[TerminalInterface.DisassemblyLines];
        private readonly byte[] disassemblyByteBuffer = new byte[128];

        private int runModeInstructionCount = 0;

        public Connector(ROMMappingMode mappingMode, string romImagePath, bool skipReads)
        {
            core = new ExecutionCore(romImagePath, mappingMode, skipReads);
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
                return AnimationLoop();
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
            else if (key.Key == ConsoleKey.F4)
            {
                // Dump memory
            }
            else if (key.Key == ConsoleKey.F12)
            {
                if (memoryViewerForm != null)
                {
                    return true;
                }
                else
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
            }
            else if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                return false;
            }

            return true;
        }

        private bool AnimationLoop()
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
                else if (animateKey.Key == ConsoleKey.F8 && memoryViewerForm != null)
                {
                    terminal.RunningState = RunningState.Running;
                    memoryViewerForm.WillWaitForRepaint = true;
                }
            }
            return true;
        }

        private bool RunningLoop()
        {
            core.ExecuteSingleInstruction();
            UpdateMemoryViewerPointers();
            runModeInstructionCount += 1;

            if (runModeInstructionCount % 100 == 0 && Console.KeyAvailable)
            {
                var runKey = Console.ReadKey(intercept: true);
                if (runKey.Key == ConsoleKey.F6)
                {
                    terminal.RunningState = RunningState.Paused;
                    memoryViewerForm!.WillWaitForRepaint = false;
                    UpdateTerminal();
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
            // TODO: there are probably better ways to copy memory around
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
            terminal.Draw();
        }

        private void UpdateTerminal()
        {
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
            Disassembler.Disassemble(disassemblyByteBuffer, 0, core.IP, disassemblyBuffer);

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
