using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models.MemoryHistory;
using Celarix.JustForFun.ForeverExMemoryView;

namespace Celarix.JustForFun.ForeverEx
{
    internal static class HistoryFrameBuilder
    {
        private static readonly Color[] bytePalette;

        static HistoryFrameBuilder()
        {
            bytePalette = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                bytePalette[i] = GetColorFromByte((byte)i);
            }
        }

        public static void BuildFramesFromMemoryHistory(string historyFilePath, string outputFolderPath)
        {
            using var historyFile = new BinaryReader(File.Open(historyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            var memory = new byte[0x10000];
            var frameIndex = 0;

            while (!historyFile.BaseStream.Position.Equals(historyFile.BaseStream.Length))
            {
                Console.WriteLine($"Building frame {frameIndex}...");
                SaveMemoryFrame(memory, Path.Combine(outputFolderPath, $"{frameIndex:D6}.png"));
                frameIndex += 1;
                var currentEvent = MemoryHistoryEvent.Read(historyFile);
                currentEvent.Apply(memory);
            }
        }

        private static void SaveMemoryFrame(byte[] memory, string outputFilePath)
        {
            using var bitmap = new DirectBitmap(256, 256);

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    var address = (ushort)((y * 256) + x);
                    var value = memory[address];
                    bitmap.SetPixel(x, y, bytePalette[value]);
                }
            }

            bitmap.SaveAsPNG(outputFilePath);
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
