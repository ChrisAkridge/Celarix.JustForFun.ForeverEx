using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celarix.JustForFun.ForeverEx.Models.MemoryHistory;

namespace Celarix.JustForFun.ForeverEx
{
    internal sealed class MemoryHistoryWatcher : IDisposable
    {
        private readonly BinaryWriter writer;

        public string OutputFilePath { get; }

        public MemoryHistoryWatcher(string outputFilePath)
        {
            OutputFilePath = outputFilePath;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                writer = new BinaryWriter(File.Open(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not open the output file at {outputFilePath}.", ex);
            }
        }

        public void WriteEvent(MemoryHistoryEvent historyEvent)
        {
            historyEvent.Write(writer);
        }

        public void Dispose()
        {
            writer.Close();
            writer.Dispose();
        }
    }
}
