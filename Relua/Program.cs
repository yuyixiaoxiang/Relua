using System;
using System.IO;
using System.Text.RegularExpressions;
using Lua.AST;

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
            var context = new CheckContext();
            expr.CheckNode(context,null);
            Console.WriteLine($"{context}");
            
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
            var processor = new Processor();
            foreach (var file in resut)
            {
                if (file.FileName.Contains("LoginModule") == false)
                {
                    // continue;
                }

                var srcPath = file.FilePath;
                var requirePath =Path.GetRelativePath(topLuaDir,srcPath).Replace(Path.GetExtension(srcPath),"").Replace("\\","/");
                var outpath = srcPath.Insert(srcPath.LastIndexOf(".lua", StringComparison.Ordinal), "");
                processor.AddFile(srcPath, requirePath,outpath);
            }

            var outfiles = processor.Process();

            foreach (var outfile in outfiles)
            {
                if (File.Exists(outfile.path))
                    File.Delete(outfile.path);
                File.WriteAllText(outfile.path, outfile.content);
            }
        }
    }
}