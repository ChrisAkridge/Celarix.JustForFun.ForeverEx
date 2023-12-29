using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverExMemoryView
{
    // https://stackoverflow.com/a/34801225/2709212
    public sealed class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public int[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private GCHandle BitsHandle { get; set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new int[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color color)
        {
            int index = x + (y * Width);
            int col = color.ToArgb();
            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            int index = x + (y * Width);
            int col = Bits[index];
            return Color.FromArgb(col);
        }

        public void SaveAsPNG(string outputFilePath)
        {
            Bitmap.Save(outputFilePath, ImageFormat.Png);
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
}
