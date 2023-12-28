namespace Celarix.JustForFun.ForeverEx
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length is < 2 or > 3)
            {
                Usage();
                return;
            }

            var mappingModeArg = args[0];
            var romImagePath = args[1];
            var skipReads = args.Length == 3 && args[2] == "-s";

            var mappingMode = args[0].ToLowerInvariant() switch
            {
                "-m" => ROMMappingMode.Mapped16,
                "-o" => ROMMappingMode.OverflowShifting,
                _ => throw new ArgumentException($"Invalid mapping mode: {mappingModeArg}")
            };

            var connector = new Connector(mappingMode, romImagePath, skipReads);
            connector.Run();
        }

        private static void Usage()
        {
            Console.WriteLine("Celarix.JustForFun.ForeverEx");
            Console.WriteLine("A toy processor emulator that can \"run\" any arbitrary file as a program.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("\tCelarix.JustForFun.ForeverEx <mappingMode> <romImagePath> [-s]");
            Console.WriteLine("\tmappingMode: Either -m or -o:");
            Console.WriteLine("\t\t-m: Mapped16: Uses up to the first 1MB of the provided file as a ROM image, split into 16 32KB banks. Files under 1MB will be padded with zeroes.");
            Console.WriteLine("\t\t-o: OverflowShifting: Uses the entire provided file as a ROM image, shifting forward by a 32KB bank every time the instruction pointer reaches the end of the current bank.");
            Console.WriteLine("\t-s: Optional. If provided, automatically provides a random message when a READ instruction is executed.");
            Console.WriteLine("\tromImagePath: The path to the ROM image to use. Can be any file.");
        }

        
    }
}