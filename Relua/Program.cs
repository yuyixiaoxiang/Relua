using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Lua
{
    public static class Program
    {

        public static int Main(string[] args)
        {
            FilterAllPloopClass();
            return default;
            var testluafile = @"f:/lua2ts/infile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Console.WriteLine($"{expr}");
            var outpath = "f:/lua2ts/outputfile.lua";
            File.WriteAllText(outpath, expr.ToString());
            Console.WriteLine(outpath);
            return default;
        }

        private static void FilterAllPloopClass()
        {
            var topLuaDir = "D:\\slgframework2\\Assets\\client-code\\LuaFramework\\Lua";
            Console.WriteLine(topLuaDir);
            var resut = PloopScanner.ScanLuaFiles(topLuaDir);
            var errCnt = 0;
            foreach (var file in resut)
            {
                if (file.FileName != "GameView.lua")
                {
                    continue;
                }

                var srcPath = file.FilePath;
                var outpath =srcPath.Insert(srcPath.LastIndexOf(".lua"),"_ploop");
                try
                {
                    var tokenizer = new Tokenizer(File.ReadAllText(srcPath));
                    var parser = new Parser(tokenizer);
                    var expr = parser.Read();
                    // Console.WriteLine($"{expr}");
                    // var outpath = "f:/lua2ts/outputfile.lua";
                    if(File.Exists(outpath))
                        File.Delete(outpath);
                    File.WriteAllText(outpath, expr.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{srcPath} \n {e} \n");
                    // Console.WriteLine(outpath);
                    errCnt++;
                }
            }
            
            Console.WriteLine($"errCnt: {errCnt}");
        }
    }
}