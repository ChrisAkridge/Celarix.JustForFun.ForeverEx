using Celarix.JustForFun.ForeverEx.Disassembly;

namespace Celarix.JustForFun.ForeverEx
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length is < 2 or > 5)
            {
                Usage();
                return;
            }

            if (args[0].Equals("-m", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("-o", StringComparison.InvariantCultureIgnoreCase))
            {
                var mappingModeArg = args[0];
                var romImagePath = args[1];
                var skipReads = args.Length > 2 && args[2] == "-s";

                var mappingMode = args[0].ToLowerInvariant() switch
                {
                    "-m" => ROMMappingMode.Mapped16,
                    "-o" => ROMMappingMode.OverflowShifting,
                    _ => throw new ArgumentException($"Invalid mapping mode: {mappingModeArg}")
                };

                var dumpMemory = args.Length > 3 && args[3] == "-d";
                if (dumpMemory && args.Length != 5)
                {
                    Usage();
                    return;
                }
                var dumpMemoryPath = dumpMemory ? args[4] : null;

                var connector = new Connector(mappingMode, romImagePath, skipReads, dumpMemoryPath);
                connector.Run();
            }
            else if (args[0].Equals("-d", StringComparison.InvariantCultureIgnoreCase))
            {
                var romImagePath = args[1];
                var disassemblyOutputPath = args[2];
                FileDisassembler.DisassembleFile(romImagePath, disassemblyOutputPath);
            }
        }

        private static void Usage()
        {
            Console.WriteLine("Celarix.JustForFun.ForeverEx");
            Console.WriteLine("A toy processor emulator that can \"run\" any arbitrary file as a program.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("\tCelarix.JustForFun.ForeverEx <mappingMode> <romImagePath> [-s] [-d] [<dumpPath>]");
            Console.WriteLine("\tmappingMode: Either -m or -o:");
            Console.WriteLine("\t\t-m: Mapped16: Uses up to the first 1MB of the provided file as a ROM image, split into 16 32KB banks. Files under 1MB will be padded with zeroes.");
            Console.WriteLine("\t\t-o: OverflowShifting: Uses the entire provided file as a ROM image, shifting forward by a 32KB bank every time the instruction pointer reaches the end of the current bank.");
            Console.WriteLine("\t-s: Optional. If provided, automatically provides a random message when a READ instruction is executed.");
            Console.WriteLine("\t-d: Optional. If provided, dumps the history of memory changes into the folder at <dumpPath>, along with PNG frames of memory that can be made into a video with ffmpeg.");
            Console.WriteLine("\t\tIf -d is provided, <dumpPath> must be provided as well.");
            Console.WriteLine("\t<dumpPath>: The path to the folder to dump memory history into.");
            Console.WriteLine("\tromImagePath: The path to the ROM image to use. Can be any file.");
            Console.WriteLine("OR:");
            Console.WriteLine("\tCelarix.JustForFun.ForeverEx -d <romImagePath> <disassemblyOutputPath>");
            Console.WriteLine("\t-d: If provided as the first argument, disassembles the ROM image at <romImagePath> and writes the output to <disassemblyOutputPath>.");
            Console.WriteLine("\t<romImagePath>: The path to the ROM image to disassemble.");
            Console.WriteLine("\t<disassemblyOutputPath>: The path to write the disassembly output to.");
        }

        
    }
}