namespace Celarix.JustForFun.ForeverEx
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var terminal = new TerminalInterface();
            terminal.A = 0x7fff;
            Console.ReadKey();
        }
    }
}