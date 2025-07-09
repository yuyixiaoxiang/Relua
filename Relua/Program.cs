using System;

using System.IO;

namespace Lua {
    public static class Program {
        public static int Main(string[] args)
        {
            var testluafile = @"f:/lua2ts/infile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Console.WriteLine($"{expr}");
            var outpath = "f:/lua2ts/outputfile.lua";
            File.WriteAllText(outpath,expr.ToString());
            Console.WriteLine(outpath);
            return default;
        }
    }
}
