using System.Reflection;

namespace Celarix.JustForFun.ForeverExMemoryView
{
    public partial class MainForm : Form
    {
        private const int RAMSize = 32768;
        private const int ROMBankSize = 32768;
        private const int PixelScalingFactor = 4;
        private const int BitmapSize = 256 * PixelScalingFactor;

        private readonly Color[] bytePalette = new Color[256];

        private readonly byte[] ram = new byte[RAMSize];
        private byte[] romBank = new byte[ROMBankSize];
        private readonly DirectBitmap memoryBitmap = new DirectBitmap(BitmapSize, BitmapSize);

        public event EventHandler FormClosedByUser;

        public ushort StackPointer { get; set; }
        public ushort InstructionPointer { get; set; }
        public bool WillWaitForRepaint { get; set; }

        public MainForm(byte[] initialRAM, byte[] initialROMBank, ushort stackPointer, ushort instructionPointer)
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;

            // May God have mercy on my soul
            typeof(Panel).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, PanelMemoryView, new object[] { true });

            for (int i = 0; i < 256; i++)
            {
                bytePalette[i] = GetColorFromByte((byte)i);
            }

            Array.Copy(initialRAM, ram, initialRAM.Length);
            Array.Copy(initialROMBank, romBank, initialROMBank.Length);
            this.StackPointer = stackPointer;
            this.InstructionPointer = instructionPointer;
        }

        public void MainForm_Load(object sender, EventArgs e)
        {
            InitializeBitmapSection(0);
        }

        public void PanelMemoryView_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(memoryBitmap.Bitmap, 0, 0);

            var stackPointerRect = new Rectangle((StackPointer & 0xFF) * PixelScalingFactor,
                (StackPointer >> 8) * PixelScalingFactor, PixelScalingFactor, PixelScalingFactor);
            e.Graphics.DrawRectangle(Pens.Red, stackPointerRect);
            var instructionPointerRect = new Rectangle((InstructionPointer & 0xFF) * PixelScalingFactor,
                (InstructionPointer >> 8) * PixelScalingFactor, PixelScalingFactor, PixelScalingFactor);
            e.Graphics.DrawRectangle(Pens.Yellow, instructionPointerRect);
        }

        public void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            memoryBitmap.Dispose();
            if (e.CloseReason == CloseReason.UserClosing)
            {
                FormClosedByUser?.Invoke(this, EventArgs.Empty);
            }
        }

        private void InitializeBitmapSection(int startingY)
        {
            for (int y = startingY; y < BitmapSize; y++)
            {
                // Each byte is represented by a 4x4 pixel area
                // but we'll scan across each line to minimize cache misses
                for (int x = 0; x < BitmapSize; x += PixelScalingFactor)
                {
                    var indexColumn = x / PixelScalingFactor;
                    var indexRow = y / PixelScalingFactor;
                    var index = indexColumn + (indexRow * 256);
                    var memoryByte = (index < 0x8000)
                        ? ram[index]
                        : romBank[index - 0x8000];
                    var color = bytePalette[memoryByte];

                    // Draw 4 pixels per loop.
                    memoryBitmap.SetPixel(x, y, color);
                    memoryBitmap.SetPixel(x + 1, y, color);
                    memoryBitmap.SetPixel(x + 2, y, color);
                    memoryBitmap.SetPixel(x + 3, y, color);
                }
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    PanelMemoryView.Invalidate();

                    if (WillWaitForRepaint)
                    {
                        SharedSynchronization.SignalRepaintComplete();
                    }
                }));
            }
            else
            {
                PanelMemoryView.Invalidate();

                if (WillWaitForRepaint)
                {
                    SharedSynchronization.SignalRepaintComplete();
                }
            }
        }

        public void ReplaceROMBank(byte[] newROMBank)
        {
            romBank = newROMBank;
            InitializeBitmapSection(BitmapSize / 2);
        }

        public void RedrawChangedMemory(int address, byte newValue)
        {
            SetUnderlyingMemory(address, newValue);
            SetImagePixel(address, newValue);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    PanelMemoryView.Invalidate();

                    if (WillWaitForRepaint)
                    {
                        SharedSynchronization.SignalRepaintComplete();
                    }
                }));
            }
        }

        private void SetImagePixel(int address, byte newValue)
        {
            var bitmapY = (address >> 8) * PixelScalingFactor;
            var bitmapX = (address & 0xFF) * PixelScalingFactor;
            var color = bytePalette[newValue];

            for (int y = bitmapY; y < bitmapY + 4; y++)
            {
                for (int x = bitmapX; x < bitmapX + 4; x++)
                {
                    memoryBitmap.SetPixel(x, y, color);
                }
            }
        }

        private void SetUnderlyingMemory(int address, byte newValue)
        {
            if (address < 0x8000)
            {
                ram[address] = newValue;
            }
            else
            {
                romBank[address - 0x8000] = newValue;
            }
        }

        public void RedrawChangedMemoryRange(int address, byte[] changedMemory)
        {
            var currentAddress = address;
            for (int i = 0; i < changedMemory.Length; i++)
            {
                SetUnderlyingMemory(currentAddress, changedMemory[i]);
                SetImagePixel(currentAddress, changedMemory[i]);
                currentAddress += 1;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    PanelMemoryView.Invalidate();

                    if (WillWaitForRepaint)
                    {
                        SharedSynchronization.SignalRepaintComplete();
                    }
                }));
            }
        }

        public void SetPointers(ushort stackPointer, ushort instructionPointer)
        {
            StackPointer = stackPointer;
            InstructionPointer = instructionPointer;
            BeginInvoke(new Action(() =>
            {
                PanelMemoryView.Invalidate();

                if (WillWaitForRepaint)
                {
                    SharedSynchronization.SignalRepaintComplete();
                }
            }));
        }

        private static Color GetColorFromByte(byte value)
        {
            const int oneSeventhOf255 = 255 / 7;
            const int oneThirdOf255 = 255 / 3;

            var r = Math.Clamp((value >> 5) * oneSeventhOf255, 0, 255);
            var g = Math.Clamp(((value & 0b00011100) >> 2) * oneSeventhOf255, 0, 255);
            var b = Math.Clamp((value & 0b00000011) * oneThirdOf255, 0, 255);

            return Color.FromArgb(r, g, b);
        }
    }
}