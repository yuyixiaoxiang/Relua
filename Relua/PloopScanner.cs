using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class PloopScanner
{
    // 正则表达式模式
    private static readonly Regex ModulePattern = new Regex(
        @"Module\s+""([^""]+)""\s*\(\s*function\s*\(\s*_ENV\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    private static readonly Regex ClassPattern = new Regex(
        @"class\s+""([^""]+)""\s*\(\s*function\s*\(\s*_ENV\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    // 需要跳过的目录名称
    private static readonly List<string> SkipPaths = new List<string>()
    {
        "3rd",
        "DataTable",
        "GameNet\\Core",
    };

    public static List<ScanResult> ScanLuaFiles(string rootPath)
    {
        var results = new List<ScanResult>();
        int totalFiles = 0;
        int matchedFiles = 0;

        try
        {
            Console.WriteLine("🔍 正在搜索 Lua 文件...");

            // 获取所有 .lua 文件，并过滤掉需要跳过的目录
            var luaFiles = Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Where(file => !ShouldSkipFile(file))
                .OrderBy(file => file)
                .ToArray();

            totalFiles = luaFiles.Length;
            Console.WriteLine($"✅ 找到 {totalFiles} 个 Lua 文件");
            Console.WriteLine();

            if (totalFiles == 0)
            {
                Console.WriteLine("⚠️  没有找到任何 Lua 文件。");
                return results;
            }

            // 进度显示
            int processedCount = 0;
            foreach (var filePath in luaFiles)
            {
                processedCount++;

                // 显示进度
                if (processedCount % 10 == 0 || processedCount == totalFiles)
                {
                    var progress = (double)processedCount / totalFiles * 100;
                    Console.Write($"\r⏳ 扫描进度: {processedCount}/{totalFiles} ({progress:F1}%)");
                }

                var fileResults = ScanSingleFile(filePath);

                if (fileResults.Any())
                {
                    matchedFiles++;
                    results.AddRange(fileResults);
                    Console.WriteLine();
                    Console.WriteLine($"✅ {GetRelativePath(rootPath, filePath)} - 找到 {fileResults.Count} 个匹配");
                }
            }

            Console.WriteLine();
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"❌ 权限不足: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.WriteLine($"❌ 目录不存在: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 扫描过程中发生错误: {ex.Message}");
        }

        Console.WriteLine("\n" + "=＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝");
        Console.WriteLine("📊 扫描统计:");
        Console.WriteLine($"   总计扫描文件: {totalFiles} 个");
        Console.WriteLine($"   匹配文件数量: {matchedFiles} 个");
        Console.WriteLine($"   找到匹配项: {results.Count} 个");
        Console.WriteLine("=＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝");

        return results;
    }

    private static bool ShouldSkipFile(string filePath)
    {
        try
        {
            if (Path.GetFileNameWithoutExtension(filePath).EndsWith("_ploop"))
                return true;
            // 获取文件路径中的所有目录部分
            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directoryPath))
                return false;
            var skip = false;
            SkipPaths.ForEach((s =>
            {
                if(filePath.Contains(s))
                    skip = true;
            }));

            if (skip)
            {
                Console.WriteLine($"------------------------skip: {filePath}");
            }

            return skip;
        }
        catch
        {
            return false;
        }
    }

    public class ScanResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public List<string> MatchTypes { get; set; } = new List<string>(); // 改为列表
        public List<string> Names { get; set; } = new List<string>(); // 改为列表
        public List<string> FullMatches { get; set; } = new List<string>(); // 改为列表
        public List<int> LineNumbers { get; set; } = new List<int>(); // 改为列表
        public List<string> LineContents { get; set; } = new List<string>(); // 改为列表

        // 便民属性和方法
        public int TotalMatches => MatchTypes.Count;
        public bool HasModule => MatchTypes.Contains("Module");
        public bool HasClass => MatchTypes.Contains("Class");

        public void AddMatch(string matchType, string name, string fullMatch, int lineNumber, string lineContent)
        {
            MatchTypes.Add(matchType);
            Names.Add(name);
            FullMatches.Add(fullMatch);
            LineNumbers.Add(lineNumber);
            LineContents.Add(lineContent);
        }

        public override string ToString()
        {
            var types = string.Join(", ", MatchTypes.Distinct());
            return $"{FileName} - {types} ({TotalMatches} 个匹配)";
        }
    }

    private static List<ScanResult> ScanSingleFile(string filePath)
    {
        var results = new List<ScanResult>();

        try
        {
            var content = File.ReadAllText(filePath);
            var lines = File.ReadAllLines(filePath);

            // 检查是否有匹配项
            var moduleMatches = ModulePattern.Matches(content);
            var classMatches = ClassPattern.Matches(content);

            if (moduleMatches.Count == 0 && classMatches.Count == 0)
            {
                return results; // 没有匹配项，直接返回
            }

            // 创建单个 ScanResult 对象
            var scanResult = new ScanResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            // 添加 Module 匹配项
            foreach (Match match in moduleMatches)
            {
                var lineNumber = GetLineNumber(content, match.Index);
                var lineContent = lineNumber > 0 && lineNumber <= lines.Length ? lines[lineNumber - 1].Trim() : "";

                scanResult.AddMatch(
                    "Module",
                    match.Groups[1].Value,
                    match.Value,
                    lineNumber,
                    lineContent
                );
            }

            // 添加 Class 匹配项
            foreach (Match match in classMatches)
            {
                var lineNumber = GetLineNumber(content, match.Index);
                var lineContent = lineNumber > 0 && lineNumber <= lines.Length ? lines[lineNumber - 1].Trim() : "";

                scanResult.AddMatch(
                    "Class",
                    match.Groups[1].Value,
                    match.Value,
                    lineNumber,
                    lineContent
                );
            }

            // 按行号排序匹配项
            SortMatchesByLineNumber(scanResult);

            results.Add(scanResult);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"⚠️  权限不足，跳过文件: {Path.GetFileName(filePath)}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"⚠️  读取文件失败 {Path.GetFileName(filePath)}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  处理文件出错 {Path.GetFileName(filePath)}: {ex.Message}");
        }

        return results;
    }

// 新增：按行号排序匹配项的辅助方法
    private static void SortMatchesByLineNumber(ScanResult scanResult)
    {
        // 创建索引数组并按行号排序
        var indices = Enumerable.Range(0, scanResult.LineNumbers.Count)
            .OrderBy(i => scanResult.LineNumbers[i])
            .ToArray();

        // 重新排列所有列表
        scanResult.MatchTypes = indices.Select(i => scanResult.MatchTypes[i]).ToList();
        scanResult.Names = indices.Select(i => scanResult.Names[i]).ToList();
        scanResult.FullMatches = indices.Select(i => scanResult.FullMatches[i]).ToList();
        scanResult.LineNumbers = indices.Select(i => scanResult.LineNumbers[i]).ToList();
        scanResult.LineContents = indices.Select(i => scanResult.LineContents[i]).ToList();
    }


    private static int GetLineNumber(string content, int charIndex)
    {
        if (charIndex < 0 || charIndex >= content.Length)
            return 1;

        return content.Substring(0, charIndex).Count(c => c == '\n') + 1;
    }

    private static string GetRelativePath(string basePath, string fullPath)
    {
        try
        {
            var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
        }
        catch
        {
            return fullPath;
        }
    }
}