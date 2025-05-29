using System;

using System.IO;

namespace Relua {
    public static class Program {
        public static int Main(string[] args)
        {
            Console.WriteLine("----");
            var testluafile = @"f:/lua2ts/infile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Console.WriteLine($"{expr}");
            var outpath = "f:/lua2ts/outputfile.ts";
            File.WriteAllText(outpath,expr.ToString());
            Console.WriteLine(outpath);
            
            // outpath = "f:/lua2ts/outputfile_ts.lua";
            // File.WriteAllText(outpath,expr.toTSSTring());
            // Console.WriteLine(outpath);
            return default;
        }
    }
}
