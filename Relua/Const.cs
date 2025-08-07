using System.Text.RegularExpressions;

namespace Lua;

public class Const
{
    public static string topLuaDir = "D:\\e-u3dclient\\Assets\\client-code\\LuaFramework\\Lua";



    public static List<string> DataNames
    {
        get
        {
            //处理GameData
            var luaFilePath = Path.Combine(Const.topLuaDir, "GameData/init.lua");
            // 读取Lua文件内容
           var  luaContent = File.ReadAllText(luaFilePath);
            // 匹配ModuleNames表内容的正则
            var pattern = @"DataNames\s*=\s*{([\s\S]*?)}";

            var match = Regex.Match(luaContent, pattern);

            List<string> dataNames = new List<string>();
            if (match.Success)
            {
                // 获取表中的值
                string tableContent = match.Groups[1].Value;

                // 根据逗号和换行分割值，并去掉空格和引号
                string[] values = tableContent
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var value in values)
                {
                    // 去掉两侧的空格、换行符以及引号
                    string cleanedValue = value
                        .Trim() // 修剪空格及换行符
                        .Trim('\"') // 去掉双引号
                        .Replace("\r", "")
                        .Replace("\n", "");
                    // Console.WriteLine(cleanedValue);
                    if (string.IsNullOrEmpty(cleanedValue) == false)
                        dataNames.Add(cleanedValue);
                }
            }
            return dataNames;
        }
    }
}