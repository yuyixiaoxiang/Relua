using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lua.AST;

namespace Lua
{
    public static class Program
    {
        
        public static int Main(string[] args)
        {
            ProcessAllPloopClass();
            return default;
            var testluafile = @"f:/lua2ts/infile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            expr.ToString();
            var context = new CheckContext();
            expr.CheckNode(context,null);
            expr.ExportableVariables(context);
            Console.WriteLine($"{context}");
            
            var outpath = "f:/lua2ts/outputfile.lua";
            File.WriteAllText(outpath, expr.ToString());
            Console.WriteLine(outpath);
            return default;
        }

        private static List<string> filterPaths = new List<string>()
        {
            "GamePlay/GameModule/Login/LoginModule",
            "GameModule/NetModule/NetStateModule",
            "GameModule/NetModule/ReconnectNetModul",
            "GameModule/ResendMsg/ResendMsgModule",
            "GameModule/Payment/PaymentModule",
            "GameModule/MainModule",
            // "GameModule/WorldMapModule",
            // "GameData/EnumData",
            // "MapViewPortChangedHandler",
            // "MapCityObjectComponent",
            // "MapObjectComponent",
            // "MapNpcObjectComponent",
            // "MapArmyObjectComponent",
            // "GameData/Map/WorldMapData"
        };

        private static List<string> fullcopyfiles = new List<string>()
        {
            "Procedure.lua",
            "GameModule.lua",
        };
            
        
        private static void ProcessAllPloopClass()
        {
            var topLuaDir = "D:\\e-u3dclient\\Assets\\client-code\\LuaFramework\\Lua";
            Console.WriteLine(topLuaDir);
            var scanLuaFiles = PloopScanner.ScanLuaFiles(topLuaDir);
            var errCnt = 0;
            var processor = new Processor();
            foreach (var file in scanLuaFiles)
            { 
                var srcPath = file.FilePath;
                var requirePath =Path.GetRelativePath(topLuaDir,srcPath).Replace(Path.GetExtension(srcPath),"").Replace("\\","/");
                var outpath = srcPath.Insert(srcPath.LastIndexOf(".lua", StringComparison.Ordinal), "");
                if (fullcopyfiles.Exists(s => Path.GetFileName(outpath) == s))
                {
                    Console.WriteLine($"full copy file: {outpath}");
                    var copyfile =Path.Combine(GetProjectDirectory(),"fullcopylua",Path.GetFileName(outpath));
                    var content = File.ReadAllText(copyfile);
                    File.WriteAllText(outpath, content);
                    continue;
                }

                processor.AddFile(srcPath, requirePath,outpath);
            }

            var outfiles = processor.Process();

            // return;
            foreach (var outfile in outfiles)
            {
                if(filterPaths.Exists((s => outfile.path.Replace("\\","/").Contains(s))) == false)
                    continue;
                if (File.Exists(outfile.path))
                    File.Delete(outfile.path);
                Console.WriteLine($"rewriting {outfile.path}");
                File.WriteAllText(outfile.path, outfile.content); 
            }
        }
        
        private static bool HasProjectFile(string directory)
        {
            return Directory.GetFiles(directory, "*.sln").Length > 0;
        }
        
        public static string GetProjectDirectory([CallerFilePath]string callerFilePath = "")
        {
            // 从调用此方法的源文件路径推断项目目录
            string directory = Path.GetDirectoryName(callerFilePath);
        
            // 向上查找到项目根目录（包含 .csproj 文件的目录）
            while (directory != null && !HasProjectFile(directory))
            {
                directory = Directory.GetParent(directory)?.FullName;
            }
        
            return directory;
        }
    }
}