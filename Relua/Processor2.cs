using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lua.AST;

namespace Lua;

public class Processor2
{
    /// <summary>
    /// lua files and ast 
    /// </summary>
    public class LuaArtifact
    {
        public string srcPath;
        public string fileName;
        public string requirePath;
        public string outPath;
        public Block Block;
        public ICheckContext CheckContext;
    }

    private List<LuaArtifact> files = new List<LuaArtifact>();

    public void AddFile(string src, string requirePath, string dest)
    {
        if (files.Any(x => x.srcPath == src))
            return;
        files.Add(new LuaArtifact()
        {
            srcPath = src, requirePath = requirePath, outPath = dest, fileName = Path.GetFileNameWithoutExtension(src)
        });
    }

    /// <summary>
    /// custom process file 
    /// </summary>
    /// <param name="filePath"></param>
    private void PreprocessFile(string filePath)
    {
        if (filePath.EndsWith("GameModule.lua"))
        {
            // 读取Lua文件内容
            string luaContent = File.ReadAllText(filePath);

            // 匹配 for...end 块的正则表达式
            string pattern = @"for\s+\w+,\s+\w+\s+in\s+ipairs\(ModuleNames\)\s+do[\s\S]*?end";

            // 使用正则表达式提取
            Match match = Regex.Match(luaContent, pattern);

            if (match.Success)
            {
                string extractedCode = match.Value;
                string modifiedContent = Regex.Replace(luaContent, pattern, "", RegexOptions.Singleline);
                File.WriteAllText(filePath, modifiedContent);
            }
        }

        if (filePath.EndsWith("GameData.lua"))
        {
            // 读取Lua文件内容
            string luaContent = File.ReadAllText(filePath);

            // 匹配 for...end 块的正则表达式
            string pattern = @"for\s+\w+,\s+\w+\s+in\s+ipairs\(DataNames\)\s+do[\s\S]*?end";

            // 使用正则表达式提取
            Match match = Regex.Match(luaContent, pattern);

            if (match.Success)
            {
                string extractedCode = match.Value;
                string modifiedContent = Regex.Replace(luaContent, pattern, "", RegexOptions.Singleline);
                File.WriteAllText(filePath, modifiedContent);
            }
        }

        if (filePath.EndsWith("Core.lua"))
        {
            // 读取Lua文件内容
            string luaContent = File.ReadAllText(filePath);

            // 匹配 for...end 块的正则表达式
            string pattern = @"for\s+\w+,\s+\w+\s+in\s+ipairs\(NetNames\)\s+do[\s\S]*?end";

            // 使用正则表达式提取
            Match match = Regex.Match(luaContent, pattern);

            if (match.Success)
            {
                string extractedCode = match.Value;
                string modifiedContent = Regex.Replace(luaContent, pattern, "", RegexOptions.Singleline);
                File.WriteAllText(filePath, modifiedContent);
            }
        }
    }

    /// <summary>
    /// process all lua files to ast
    /// </summary>
    /// <returns></returns>
    public List<(string path, string content)> Process()
    {
        foreach (var file in files)
        {
            try
            {
                // if(file.outPath.Contains("EntityMenuModule.lua") == false)
                //     continue;
                PreprocessFile(file.srcPath);
                var tokenizer = new Tokenizer(File.ReadAllText(file.srcPath));
                var parser = new Parser(tokenizer);
                var expr = parser.Read();
                //强制调用一下
                expr.ToString();
                var context = new CheckContext();
                expr.CheckNode(context, null);
                expr.ExportableVariables(context);
                file.Block = expr;
                file.CheckContext = context;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        //post process
        // ProcessSafeRequireFile();
        PreprocessModuleAndClass();
        PostprocessModuleAndPartialClass();
        // PostProcessMainSingletonClass();
        PostProcessDynamicClass();
        
        
        //outout 
        List<(string path, string content)> outputs = new List<(string path, string content)>();
        foreach (var file in files)
        {
            outputs.Add((
                path: file.outPath,
                content: file.Block.ToString()
            ));
        }
        
        return outputs;
    }

    private void ProcessSafeRequireFile()
    {
        var removerequire = new List<IStatement>();
        foreach (var file in files)
        {
            removerequire.Clear();
            for (var i = 0; i < file.Block.Statements.Count; i++)
            {
                var statement = file.Block.Statements[i];
                if (statement is FunctionCall functionCall)
                {
                    if (functionCall.Function is Variable variable)
                    {
                        if (variable.Name == "require")
                        {
                            removerequire.Add(statement);
                            continue;
                            var requirePath = functionCall.Arguments[0] as StringLiteral;
                            file.Block.Statements[i] = new If()
                            {
                                MainIf = new ConditionalBlock()
                                {
                                    Block = new Block()
                                    {
                                        Statements = new List<IStatement>()
                                        {
                                            functionCall
                                        }
                                    },
                                    Condition = new UnaryOp()
                                    {
                                        Type = UnaryOp.OpType.Invert,
                                        Expression = new TableAccess()
                                        {
                                            Table = new TableAccess()
                                            {
                                                Table = new Variable()
                                                {
                                                    Name = "package",
                                                },
                                                Index = new StringLiteral()
                                                {
                                                    Value = "loaded"
                                                }
                                            },
                                            Index = new StringLiteral()
                                            {
                                                Value = requirePath.Value
                                            }
                                        }
                                    }
                                }
                            };
                        }
                    }
                }
            }

            file.Block.Statements.RemoveAll((statement => removerequire.Contains(statement)));
        }
    }

    /// <summary>
    /// define module/class aggregation
    /// </summary>
    public class ModuleAndClass
    {
        public LuaArtifact file;
        public List<PloopModule> Modules = new List<PloopModule>();
        public Ploop Ploop;
        public List<PloopClass> Classes = new List<PloopClass>();
    }

    List<ModuleAndClass> moduleAndClasses = new List<ModuleAndClass>();

    private void PreprocessModuleAndClass()
    {
        //process partial class

        foreach (var file in files)
        {
            Debug.Assert(file.Block != null, nameof(file.Block) + " != null");
            //skip the empty file 
            if (file.Block.Statements.Count <= 0)
                continue;
            List<IStatement> allmodules = file.Block.Statements.FindAll((statement => statement is PloopModule));
            Ploop? ploop = file.Block.Statements.Find((statement => statement is Ploop)) as Ploop;

            var moduleAndClasse = new ModuleAndClass();
            moduleAndClasse.file = file;
            //find all the ploop class 
            List<IStatement> allPloopClasses = file.Block.Statements.FindAll((statement => statement is PloopClass));

            if (ploop != null)
            {
                moduleAndClasse.Ploop = ploop;
                allPloopClasses.AddRange(ploop.Statements.FindAll((statement => statement is PloopClass)));
            }

            foreach (var module in allmodules)
            {
                var module_ = module as PloopModule;
                moduleAndClasse.Modules.Add(module_);
                allPloopClasses.AddRange(module_.Statements.FindAll((statement => statement is PloopClass)));
            }

            //process multiply class
            var singleFileMultiClass = allPloopClasses.Count > 1;
            foreach (var tmpclass in allPloopClasses)
            {
                var ploopClass = tmpclass as PloopClass;
                ploopClass.RequirePath = file.requirePath;
                ploopClass.FileName = file.fileName;
                ploopClass.singleFileMultiClass = singleFileMultiClass;
                moduleAndClasse.Classes.Add(ploopClass);
            }

            if (singleFileMultiClass)
            {
                var tmpLocalClassIndex = 0;
                //在顶部声明所有的类的local 变量
                foreach (var tmpclass in allPloopClasses)
                {
                    var ploopClass = tmpclass as PloopClass;
                    var className = ploopClass.ClassName;
                    file.Block.Statements.Insert(tmpLocalClassIndex++, new Assignment()
                    {
                        IsLocal = true,
                        Targets = new List<IAssignable>()
                        {
                            new Variable()
                            {
                                Name = className,
                            }
                        }
                    });
                }
            }


            if (singleFileMultiClass)
            {
                // Console.WriteLine($"Multi Class File: {file.srcPath}");
                // file.Block.Statements.Add(new Return()
                // {
                //     Expressions = new List<IExpression>()
                //     {
                //         new TableConstructor()
                //         {
                //             Entries = moduleAndClasse.Classes.ConvertAll<TableConstructor.Entry>((input =>
                //                 new TableConstructor.Entry()
                //                 {
                //                     ExplicitKey = true,
                //                     Key = new StringLiteral() { Value = input.ClassName },
                //                     Value = new Variable() { Name = input.ClassName },
                //                 }))
                //         }
                //     }
                // });
            }
            else
            {
                // if (moduleAndClasse.Classes.Count > 0)
                // {
                //     file.Block.Statements.Add(new Return()
                //     {
                //         Expressions = new List<IExpression>()
                //         {
                //             new Variable() { Name = moduleAndClasse.Classes[0].ClassName }
                //         }
                //     });
                // }
            }

            moduleAndClasses.Add(moduleAndClasse);
        }
    }

    /// <summary>
    /// post process module and class
    /// </summary>
    /// <exception cref="Exception"></exception>
    private void PostprocessModuleAndPartialClass()
    {
        //test same name ploop class 
        var sameclassKeys = new Dictionary<string, List<PloopClass>>();
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                var classname = _ploopClass.ClassName;
                if (sameclassKeys.ContainsKey(classname) == false)
                    sameclassKeys.Add(classname, new List<PloopClass>());
                sameclassKeys[classname].Add(_ploopClass);
            }
        }

        var sameClassNameButNotPartial = new List<PloopClass>();
        // var partialClasses = new List<PloopClass>();
        foreach (var kv in sameclassKeys)
        {
            if (kv.Value.Count < 2)
                continue;
            Console.WriteLine($"{kv.Key}");
            var isPartialClass = false;
            PloopClass mainPartialClass = null;
            List<PloopClass> subPartialClasses = new List<PloopClass>();
            foreach (var _class in kv.Value)
            {
                Console.WriteLine($"=>{_class.RequirePath}");
                //CUSTOM
                if (_class.ClassName == "CfgCondition")
                {
                    if (_class.FileName == "CfgConditionBind")
                    {
                        mainPartialClass = _class;
                    }
                    else
                    {
                        subPartialClasses.Add(_class);
                    }

                    continue;
                }

                if (_class.ClassName == _class.FileName)
                {
                    mainPartialClass = _class;
                }
                else
                {
                    //CUSTOM  
                    // if (_class.FileName == "CfgCondition")
                    // {
                    //     subPartialClasses.Add(_class);
                    //     continue;
                    // }

                    string prefix = $"{_class.ClassName}";
                    //CUSTOM 
                    if (kv.Key == "ActionSystem")
                        prefix = "Action";
                    if (kv.Key == "ConditionSystem")
                        prefix = "Condition_";

                    if (_class.FileName.StartsWith(prefix))
                    {
                        subPartialClasses.Add(_class);
                    }

                    //CUSTOM
                    // if (kv.Key == "MapCityNodeView" && _class.FileName == "CityBuildingView_Scaffold")
                    // {
                    //     subPartialClasses.Add(_class);
                    // }
                    
                    if (kv.Key == "ConditionBase" && _class.FileName == "ConditionJump")
                    {
                        subPartialClasses.Add(_class);
                    }
                }
            }

            if (mainPartialClass != null && subPartialClasses.Count > 0)
            {
                isPartialClass = true;
            }

            if (isPartialClass == false)
            {
                sameClassNameButNotPartial.AddRange(kv.Value);
            }

            //
            if (isPartialClass)
            {
                mainPartialClass.IsPartialClass = true;
                mainPartialClass.IsMainPartialClass = true;
                foreach (var subPartialClass in subPartialClasses)
                {
                    subPartialClass.IsPartialClass = true;
                    subPartialClass.MainPartialClass = mainPartialClass;
                    subPartialClass.MainPartialRequirePath = mainPartialClass.RequirePath;

                    mainPartialClass.SubPartialRequirePaths.Add(subPartialClass.RequirePath);
                }
            }
        }

        Console.WriteLine($"-------------------------------sameClassNameButNotPartial---------------");
        foreach (var _class in sameClassNameButNotPartial)
        {
            Console.WriteLine($"{_class.ClassName} {_class.RequirePath}");
        }

        Console.WriteLine($"-------------------------------sameClassNameButNotPartial---------------");


        //process inheriate
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                var inheritClassName = _ploopClass.InheritClassName;
                if (string.IsNullOrEmpty(inheritClassName) == false)
                {
                    //CUSTOM
                    if (inheritClassName == "Common.StateMachine.IContext")
                    {
                        inheritClassName = "IContext";
                        _ploopClass.InheritClassName = inheritClassName;
                    }

                    if (inheritClassName == "Common.StateMachine.IState")
                    {
                        inheritClassName = "IState";
                        _ploopClass.InheritClassName = inheritClassName;
                    }


                    Debug.Assert(inheritClassName.Split(".").Length == 1, "inheritClassName.Split('.').Length == 1");
                    if (sameclassKeys.ContainsKey(inheritClassName))
                    {
                        if (sameclassKeys[inheritClassName].Count > 1)
                        {
                            var classes = sameclassKeys[inheritClassName];
                            var partialClass = true;
                            foreach (var _class in classes)
                            {
                                if (_class.IsPartialClass == false)
                                    partialClass = false;
                            }

                            if (partialClass == false)
                                throw new Exception($"find multy base class,{_ploopClass.InheritClassName}");
                        }

                        PloopClass inheritClass = null;
                        foreach (var _inheritClass in sameclassKeys[inheritClassName])
                        {
                            if (_inheritClass.IsPartialClass == false)
                            {
                                inheritClass = _inheritClass;
                                break;
                            }
                            else if (_inheritClass.IsMainPartialClass)
                            {
                                inheritClass = _inheritClass;
                                break;
                            }
                        }

                        Debug.Assert(inheritClass != null, "inheritClass is null");
                        _ploopClass.InheritRequirePath = inheritClass.RequirePath;
                        _ploopClass.InheritClass = inheritClass;
                    }
                    else
                    {
                        throw new Exception($"not find base class,{_ploopClass.InheritClassName}");
                    }
                }
            }
        }
        
        //check Nested class 
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                List<IStatement> nestedclass = _ploopClass.Statements.FindAll((statement => statement is PloopClass));
                Debug.Assert(nestedclass.Count == 0, $"{_ploopClass.RequirePath},nestedclass.Count == 0");
            }
        }
        
        //处理自动导入
        HashSet<string> autoRequireNotFind = new HashSet<string>();
        foreach (var file in files)
        {
            // Console.WriteLine($"Auto Require File:{file.outPath}");
            // Console.WriteLine(file.CheckContext);
            // Console.WriteLine("\n\n");

            var needRequireFile = new Dictionary<string, List<ModuleAndClass>>();
            var needRequreStrs = file.CheckContext.GetRequiredVariables();
            var needAutoRequirePaths = new HashSet<string>();
            
            //首先处理基类的导入
            foreach (var moduleAndClass in moduleAndClasses)
            {
                if (moduleAndClass.file == file)
                {
                    foreach (var _ploopClass in moduleAndClass.Classes)
                    {
                        if (_ploopClass.InheritClass != null)
                        {
                            needAutoRequirePaths.Add(_ploopClass.InheritRequirePath);
                        }
                    }
                    break;
                }
            }
            
            foreach (var _needRequreStr in needRequreStrs)
            {
                var needRequreStr = _needRequreStr;

                foreach (var moduleAndClass in moduleAndClasses)
                {
                    var exportableVariables = moduleAndClass.file.CheckContext.GetExportableVariables();
                    foreach (var exportVariable in exportableVariables)
                    {
                        if (exportVariable.VariableName == needRequreStr &&
                            (exportVariable.IsClass || exportVariable.IsEnum ||
                             (exportVariable.IsField && !exportVariable.IsClassField) ||
                             (exportVariable.IsMethod && !exportVariable.IsClassMethod)))
                        {
                            if (needRequireFile.ContainsKey(needRequreStr) == false)
                                needRequireFile[needRequreStr] = new List<ModuleAndClass>();
                            if (needRequireFile[needRequreStr].Contains(moduleAndClass) == false)
                                //find 
                                needRequireFile[needRequreStr].Add(moduleAndClass);
                        }
                    }
                }
            }

            var autoRequireIndex = 0;
            foreach (var needRequreStr in needRequreStrs)
            {
                if (needRequireFile.ContainsKey(needRequreStr))
                {
                    if (needRequireFile[needRequreStr].Count > 1)
                    {
                        Console.WriteLine($"AutoRequire,find multiply:{file.outPath} {needRequreStr}");
                    }
                    //尝试自动require lua file 

                    var requireFile = needRequireFile[needRequreStr][0];
                    foreach (var _moduleAndClass in needRequireFile[needRequreStr])
                    {
                        foreach (var _class in _moduleAndClass.Classes)
                        {
                            if (_class.IsMainPartialClass)
                            {
                                requireFile = _moduleAndClass;
                                break;
                            }
                        }
                    }

                    if (requireFile.file == file)
                        continue;

                    // PloopClass needRequireClass = null;
                    // foreach (var _ploopClass in requireFile.Classes)
                    // {
                    //     if (_ploopClass.ClassName == needRequreStr)
                    //     {
                    //         needRequireClass = _ploopClass;
                    //     }
                    // }

                    if (requireFile != null)
                    {
                        var requirePath = requireFile.file.requirePath;
                        
                        //CUSTOM
                        // if (file.fileName == "WorldMapModule" && (needRequreStr.EndsWith("Component") ||
                        //                                           needRequreStr.EndsWith("View") ||
                        //                                           needRequreStr.EndsWith("ChangedHandler")))
                        // {
                        //     needRequirePaths.Add(requirePath);
                        // }
                        // else
                        {
                            needAutoRequirePaths.Add(requirePath);
                        }
                    }
                }
                else
                {
                    //如果是配置表
                    if (needRequreStr.StartsWith("Conf") && File.Exists($"{Const.fromTopLuaDir}\\DataTable\\{needRequreStr}.lua"))
                    {
                        //配置表存在
                        needAutoRequirePaths.Add($"DataTable/{needRequreStr}");
                    }
                    else
                    {
                        Console.WriteLine($"AutoRequire,Not find:{file.outPath} {needRequreStr}");
                        autoRequireNotFind.Add(needRequreStr);    
                    }
                }
            }

            if (needAutoRequirePaths.Count > 0)
            {
                NeedRequireClass(file,needAutoRequirePaths.ToList());
            }
        }

        Console.WriteLine($"\n\nAutoRequireNotFind:{string.Join(",", autoRequireNotFind)}");
    }

    private void NeedRequireClass(LuaArtifact file, List<string> requirePaths)
    {
        var sourcePath = file.srcPath;
        sourcePath = sourcePath.Replace("/","\\");
        var targetPath = sourcePath.Replace(Const.fromTopLuaDir, Const.toTopLuaDir);
        
        if(targetPath.Contains("GameView") == false)
            return;
        
        
        var allLines = File.ReadAllLines(targetPath).ToList();
        for (var index = 0; index < requirePaths.Count; index++)
        {
            var requireFunc = $"require(\"{requirePaths[index]}\")";
            allLines.Insert(index,requireFunc);    
        }
        File.WriteAllLines(targetPath, allLines);
    }

    private void PostProcessMainSingletonClass()
    {
          //处理gamemodule
        string luaFilePath = Path.Combine(Const.fromTopLuaDir, "GameModule/init.lua");
        // 读取Lua文件内容
        string luaContent = File.ReadAllText(luaFilePath);
        // 匹配ModuleNames表内容的正则
        string pattern = @"ModuleNames\s*=\s*{([\s\S]*?)}";

        Match match = Regex.Match(luaContent, pattern);

        List<string> moduleNames = new List<string>();
        if (match.Success)
        {
            // 获取表中的值
            string tableContent = match.Groups[1].Value;

            // 根据逗号和换行分割值，并去掉空格和引号
            string[] values = tableContent
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine("Lua表中的值如下：");
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
                    moduleNames.Add(cleanedValue);
            }
        }
        else
        {
            Console.WriteLine("未找到ModuleNames表内容！");
        }

        if (moduleNames.Count > 0)
        {
            var gameModule = files.Find((artifact => artifact.fileName == "GameModule"));
            var ploopModule = gameModule.Block.Statements.Find((statement => statement is PloopModule)) as PloopModule;
            var @class = ploopModule.Statements.Find((statement => statement is PloopClass)) as PloopClass;
            foreach (var statement in @class.Statements)
            {
                if (statement is Assignment assignment && assignment.Targets.Exists((assignable =>
                    {
                        if (assignable is Variable variable && variable.Name == "Init")
                        {
                            return true;
                        }

                        return false;
                    })))
                {
                    var functionDeclaration = assignment.Values[0] as FunctionDefinition;
                    for (var i = 0; i < moduleNames.Count; i++)
                    {
                        var moduleName = moduleNames[i] + "Module";
                        var moduleRequirePath = string.Empty;
                        bool singleFileMultiplyClass = false;
                        foreach (var moduleAndClass in moduleAndClasses)
                        {
                            var find = false;
                            var exportableVariables = moduleAndClass.file.CheckContext.GetExportableVariables();
                            foreach (var exportVariable in exportableVariables)
                            {
                                if (exportVariable.VariableName == moduleName && exportVariable.IsClass)
                                {
                                    foreach (var _class in moduleAndClass.Classes)
                                    {
                                        if (_class.ClassName == moduleName &&
                                            (_class.IsMainPartialClass || !_class.IsPartialClass))
                                        {
                                            find = true;
                                            moduleRequirePath = _class.RequirePath;
                                            singleFileMultiplyClass = _class.singleFileMultiClass;
                                            break;
                                        }
                                    }
                                }

                                if (find)
                                    break;
                            }

                            if (find)
                                break;
                        }

                        functionDeclaration.Block.Statements.Insert(i, new Assignment()
                        {
                            Targets = new()
                            {
                                new TableAccess()
                                {
                                    Table = new Variable()
                                    {
                                        Name = "self"
                                    },
                                    Index = new StringLiteral()
                                    {
                                        Value = moduleNames[i],
                                    }
                                }
                            },

                            Values = singleFileMultiplyClass == false
                                ? new List<IExpression>()
                                {
                                    new FunctionCall()
                                    {
                                        Function = new FunctionCall()
                                        {
                                            Arguments = new List<IExpression>()
                                            {
                                                new StringLiteral()
                                                {
                                                    Value = moduleRequirePath
                                                }
                                            },
                                            Function = new Variable()
                                            {
                                                Name = "require",
                                            }
                                        }
                                    }
                                }
                                : new List<IExpression>()
                                {
                                    new FunctionCall()
                                    {
                                        Function = new TableAccess()
                                        {
                                            Table = new FunctionCall()
                                            {
                                                Arguments = new List<IExpression>()
                                                {
                                                    new StringLiteral()
                                                    {
                                                        Value = moduleRequirePath
                                                    }
                                                },
                                                Function = new Variable()
                                                {
                                                    Name = "require",
                                                }
                                            },
                                            Index = new StringLiteral()
                                            {
                                                Value = moduleName
                                            }
                                        }
                                    }
                                }
                        });
                    }
                }


                if (statement is Assignment assignment1 && assignment1.Targets.Exists((assignable =>
                    {
                        if (assignable is Variable variable && variable.Name == "OnEnterGame")
                        {
                            return true;
                        }

                        return false;
                    })))
                {
                    var functionDeclaration = assignment1.Values[0] as FunctionDefinition;
                    functionDeclaration.Block.Statements.Insert(0, new GenericFor()
                    {
                        Iterator = new FunctionCall()
                        {
                            Function = new Variable()
                            {
                                Name = "ipairs"
                            },
                            Arguments = new List<IExpression>()
                            {
                                new Variable()
                                {
                                    Name = "ModuleNames"
                                }
                            }
                        },
                        VariableNames = new List<string>()
                        {
                            "i", "name"
                        },
                        Block = new Block()
                        {
                            Statements = new List<IStatement>()
                            {
                                new FunctionCall()
                                {
                                    Function = new TableAccess()
                                    {
                                        Table = new Variable()
                                        {
                                            Name = "safe"
                                        },
                                        Index = new StringLiteral()
                                        {
                                            Value = "callFunc"
                                        }
                                    },
                                    Arguments = new List<IExpression>()
                                    {
                                        new TableAccess()
                                        {
                                            Table = new Variable() { Name = "self" },
                                            Index = new Variable()
                                            {
                                                Name = "name"
                                            }
                                        },
                                        new StringLiteral()
                                        {
                                            Value = "OnEnterGame"
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }
        }


        //处理GameData
        List<string> dataNames = Const.DataNames;
        var dataClass = new List<PloopClass>();
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var ploopClass in moduleAndClass.Classes)
            {
                if (dataNames.Exists((s => $"{s}Data" == ploopClass.ClassName)) &&
                    ploopClass.RequirePath.Contains("GameData/"))
                {
                    if(ploopClass.IsMainPartialClass || !ploopClass.IsPartialClass)
                        dataClass.Add(ploopClass);
                }
            }
        }
        
        var dataClassAssignment = new Assignment()
        {
            // IsLocal = true,
            Targets = new List<IAssignable>()
            {
                new TableAccess()
                {
                    Table = new Variable(){Name = "self"},
                    Index = new StringLiteral(){Value = "_ENV"}
                }
            },
            Values = new List<IExpression>()
            {
                new TableConstructor()
                {
                    Entries = dataNames.Select(_input =>
                    {
                        PloopClass input = null;
                        foreach (var moduleAndClass in moduleAndClasses)
                        {
                            foreach (var ploopClass in moduleAndClass.Classes)
                            {
                                if ($"{_input}Data" == ploopClass.ClassName &&
                                    ploopClass.RequirePath.Contains("GameData/"))
                                {
                                    if (ploopClass.IsMainPartialClass || !ploopClass.IsPartialClass)
                                        input = ploopClass;
                                }
                                if(input != null)
                                    break;
                            }
                            if(input != null)
                                break;
                        }
                        
                        var singleclass = input.singleFileMultiClass;
                        var dataName = _input;
                        return new TableConstructor.Entry()
                        {
                            ExplicitKey = true,
                            Key = new StringLiteral() { Value = dataName },
                            Value = singleclass == false
                                                ? new FunctionCall()
                                                {
                                                    Arguments = new List<IExpression>()
                                                    {
                                                        new StringLiteral()
                                                        {
                                                            Value = input.RequirePath
                                                        }
                                                    },
                                                    Function = new Variable()
                                                    {
                                                        Name = "require",
                                                    }
                                                }
                                                : new TableAccess()
                                                {
                                                    Table = new FunctionCall()
                                                    {
                                                        Arguments = new List<IExpression>()
                                                        {
                                                            new StringLiteral()
                                                            {
                                                                Value = input.RequirePath
                                                            }
                                                        },
                                                        Function = new Variable()
                                                        {
                                                            Name = "require",
                                                        }
                                                    },
                                                    Index = new StringLiteral()
                                                    {
                                                        Value = input.ClassName
                                                    }
                                                }
                                
                        };
                    }).ToList()
                }
            }
        };
        

        if (dataNames.Count > 0)
        {
            var gameData = files.Find((artifact => artifact.fileName == "GameData"));
            var ploopModule = gameData.Block.Statements.Find((statement => statement is PloopModule)) as PloopModule;
            var @class = ploopModule.Statements.Find((statement => statement is PloopClass)) as PloopClass;
            
            // @class.Statements.Insert(0, dataClassAssignment);
            
            foreach (var statement in @class.Statements)
            {
                if (statement is Assignment assignment && assignment.Targets.Exists((assignable =>
                    {
                        if (assignable is Variable variable && variable.Name == "Init")
                        {
                            return true;
                        }
        
                        return false;
                    })))
                {
                    var functionDeclaration = assignment.Values[0] as FunctionDefinition;

                    functionDeclaration.Block.Statements.Insert(0, dataClassAssignment);
                    functionDeclaration.Block.Statements.Insert(1, new GenericFor()
                    {
                        VariableNames = new List<string>(){"i","v"},
                        Iterator =  new FunctionCall()
                        {
                            Function = new Variable()
                            {
                                Name = "pairs"
                            },
                            Arguments = new List<IExpression>()
                            {
                               new TableAccess()
                               {
                                   Table = new Variable(){Name = "self"},
                                   Index = new StringLiteral(){Value = "_ENV"}
                               }
                            }
                        },
                        Block = new Block()
                        {
                            Statements = new List<IStatement>()
                            {
                                new Assignment()
                                {
                                    Targets = new List<IAssignable>()
                                    {
                                        new TableAccess()
                                        {
                                            Table = new Variable(){Name = "self"},
                                            Index = new Variable(){Name = "i"}
                                        }
                                    },
                                    Values = new List<IExpression>()
                                    {
                                        new FunctionCall()
                                        {
                                            Arguments = new List<IExpression>(),
                                            Function = new Variable()
                                            {
                                                Name = "v"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });
                    
                    // for (var i = 0; i < dataNames.Count; i++)
                    // {
                    //     var dataName = dataNames[i] + "Data";
                    //     var moduleRequirePath = string.Empty;
                    //     bool singleFileMultiplyClass = false;
                    //     foreach (var moduleAndClass in moduleAndClasses)
                    //     {
                    //         var find = false;
                    //         var exportableVariables = moduleAndClass.file.CheckContext.GetExportableVariables();
                    //         foreach (var exportVariable in exportableVariables)
                    //         {
                    //             if (exportVariable.VariableName == dataName && exportVariable.IsClass)
                    //             {
                    //                 foreach (var _class in moduleAndClass.Classes)
                    //                 {
                    //                     if (_class.ClassName == dataName &&
                    //                         (_class.IsMainPartialClass || !_class.IsPartialClass))
                    //                     {
                    //                         find = true;
                    //                         moduleRequirePath = _class.RequirePath;
                    //                         singleFileMultiplyClass = _class.singleFileMultiClass;
                    //                         break;
                    //                     }
                    //                 }
                    //             }
                    //
                    //             if (find)
                    //                 break;
                    //         }
                    //
                    //         if (find)
                    //             break;
                    //     }
                    //
                    //     functionDeclaration.Block.Statements.Insert(i, new Assignment()
                    //     {
                    //         Targets = new()
                    //         {
                    //             new TableAccess()
                    //             {
                    //                 Table = new Variable()
                    //                 {
                    //                     Name = "self"
                    //                 },
                    //                 Index = new StringLiteral()
                    //                 {
                    //                     Value = dataNames[i],
                    //                 }
                    //             }
                    //         },
                    //
                    //         Values = singleFileMultiplyClass == false
                    //             ? new List<IExpression>()
                    //             {
                    //                 new FunctionCall()
                    //                 {
                    //                     Function = new FunctionCall()
                    //                     {
                    //                         Arguments = new List<IExpression>()
                    //                         {
                    //                             new StringLiteral()
                    //                             {
                    //                                 Value = moduleRequirePath
                    //                             }
                    //                         },
                    //                         Function = new Variable()
                    //                         {
                    //                             Name = "require",
                    //                         }
                    //                     }
                    //                 }
                    //             }
                    //             : new List<IExpression>()
                    //             {
                    //                 new FunctionCall()
                    //                 {
                    //                     Function = new TableAccess()
                    //                     {
                    //                         Table = new FunctionCall()
                    //                         {
                    //                             Arguments = new List<IExpression>()
                    //                             {
                    //                                 new StringLiteral()
                    //                                 {
                    //                                     Value = moduleRequirePath
                    //                                 }
                    //                             },
                    //                             Function = new Variable()
                    //                             {
                    //                                 Name = "require",
                    //                             }
                    //                         },
                    //                         Index = new StringLiteral()
                    //                         {
                    //                             Value = dataName
                    //                         }
                    //                     }
                    //                 }
                    //             }
                    //     });
                    // }
                }
        
        
                if (statement is Assignment assignment1 && assignment1.Targets.Exists((assignable =>
                    {
                        if (assignable is Variable variable && variable.Name == "OnEnterGame")
                        {
                            return true;
                        }
        
                        return false;
                    })))
                {
                    var functionDeclaration = assignment1.Values[0] as FunctionDefinition;
                    functionDeclaration.Block.Statements.Insert(0, new GenericFor()
                    {
                        Iterator = new FunctionCall()
                        {
                            Function = new Variable()
                            {
                                Name = "ipairs"
                            },
                            Arguments = new List<IExpression>()
                            {
                                new Variable()
                                {
                                    Name = "DataNames"
                                }
                            }
                        },
                        VariableNames = new List<string>()
                        {
                            "i", "name"
                        },
                        Block = new Block()
                        {
                            Statements = new List<IStatement>()
                            {
                                new FunctionCall()
                                {
                                    Function = new TableAccess()
                                    {
                                        Table = new Variable()
                                        {
                                            Name = "safe"
                                        },
                                        Index = new StringLiteral()
                                        {
                                            Value = "callFunc"
                                        }
                                    },
                                    Arguments = new List<IExpression>()
                                    {
                                        new TableAccess()
                                        {
                                            Table = new Variable() { Name = "self" },
                                            Index = new Variable()
                                            {
                                                Name = "name"
                                            }
                                        },
                                        new StringLiteral()
                                        {
                                            Value = "OnEnterGame"
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }
        }

        //处理GameNet
        luaFilePath = Path.Combine(Const.fromTopLuaDir, "GameNet/init.lua");
        // 读取Lua文件内容
        luaContent = File.ReadAllText(luaFilePath);
        // 匹配ModuleNames表内容的正则
        pattern = @"NetNames\s*=\s*{([\s\S]*?)}";

        match = Regex.Match(luaContent, pattern);

        List<string> netNames = new List<string>();
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
                    netNames.Add(cleanedValue);
            }
        }

        if (netNames.Count > 0)
        {
            var gameData = files.Find((artifact => artifact.fileName == "Core"));
            var ploopModule = gameData.Block.Statements.Find((statement => statement is PloopModule)) as PloopModule;
            var @class = ploopModule.Statements.Find((statement => statement is PloopClass)) as PloopClass;
            foreach (var statement in @class.Statements)
            {
                if (statement is Assignment assignment && assignment.Targets.Exists((assignable =>
                    {
                        if (assignable is Variable variable && variable.Name == "__ctor")
                        {
                            return true;
                        }

                        return false;
                    })))
                {
                    var functionDeclaration = assignment.Values[0] as FunctionDefinition;
                    for (var i = 0; i < netNames.Count; i++)
                    {
                        var netName = netNames[i];
                        var moduleRequirePath = string.Empty;
                        foreach (var moduleAndClass in moduleAndClasses)
                        {
                            var find = false;
                            var exportableVariables = moduleAndClass.file.CheckContext.GetExportableVariables();
                            foreach (var exportVariable in exportableVariables)
                            {
                                if (exportVariable.VariableName == netName && exportVariable.IsClass)
                                {
                                    foreach (var _class in moduleAndClass.Classes)
                                    {
                                        if (_class.ClassName == netName &&
                                            (_class.IsMainPartialClass || !_class.IsPartialClass))
                                        {
                                            find = true;
                                            moduleRequirePath = _class.RequirePath;
                                            break;
                                        }
                                    }
                                }

                                if (find)
                                    break;
                            }

                            if (find)
                                break;
                        }

                        functionDeclaration.Block.Statements.Insert(i, new Assignment()
                        {
                            Targets = new()
                            {
                                new TableAccess()
                                {
                                    Table = new Variable()
                                    {
                                        Name = "self"
                                    },
                                    Index = new StringLiteral()
                                    {
                                        Value = netNames[i],
                                    }
                                }
                            },

                            Values = new List<IExpression>()
                            {
                                new FunctionCall()
                                {
                                    Function = new FunctionCall()
                                    {
                                        Arguments = new List<IExpression>()
                                        {
                                            new StringLiteral()
                                            {
                                                Value = moduleRequirePath
                                            }
                                        },
                                        Function = new Variable()
                                        {
                                            Name = "require",
                                        }
                                    }
                                }
                            }
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理动态引入的类型
    /// </summary>
    private void PostProcessDynamicClass()
    {
        //处理Ui
        var uis = new List<PloopClass>();
        var entityMenus = new List<PloopClass>();
        var entityBtnDatas = new List<PloopClass>();
        var condJudgeExps = new List<PloopClass>();
        var msgItems = new List<PloopClass>();
        var cfgConditionClass = new List<PloopClass>();
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _class in moduleAndClass.Classes)
            {
                if (_class.InheritClassName == "ViewBase")
                {
                    uis.Add(_class);
                }

                if (_class.RequirePath.Contains("GameView/EntityMenu/"))
                {
                    entityMenus.Add(_class);
                }

                if (_class.InheritClassName == "EntityButtonData" || _class.ClassName == "EntityButtonData")
                {
                    entityBtnDatas.Add(_class);
                }

                if (_class.InheritClassName == "CondJudgeExpBase")
                {
                    condJudgeExps.Add(_class);
                }

                if (_class.RequirePath.Contains("GameView/Msg/mod"))
                {
                    msgItems.Add(_class);
                }

                if (_class.InheritClassName == "ConditionBase")
                {
                    cfgConditionClass.Add(_class);
                }
            }
        }

        //gameview/init
        var gameviewInitPath = Path.Combine(Const.toTopLuaDir, "GameView/init.lua");
        var gameViewInitStr = File.ReadAllText(gameviewInitPath);
        
        gameViewInitStr = gameViewInitStr.Insert(gameViewInitStr.IndexOf("-- 通用的界面，手动"),
            "do return end\n");
        File.WriteAllText(gameviewInitPath,gameViewInitStr);
        
        //ui 
        var requireUIs = uis.Where((@class => @class.ClassName != "$Templete$")).Select(input =>
        {
            var singleclass = input.singleFileMultiClass;
            return $"""
                            {input.ClassName} = "{input.RequirePath}"
                    """;
        }).ToList();
        var requireUITable = $$"""
                               
                                   local _LAZY_REQUIRE = {
                                   {{string.Join(",\n", requireUIs)}}
                                   }
                               
                               """;
        string FINDSTR = "class \"GameView\" (function(_ENV)";
        var gameViewPath = Path.Combine(Const.toTopLuaDir, "Common/GamePlay/GameView.lua");
        var gameViewStr = File.ReadAllText(gameViewPath);
        var indexOf = gameViewStr.IndexOf(FINDSTR) + FINDSTR.Length;
        gameViewStr = gameViewStr.Insert(indexOf, requireUITable);
        gameViewStr = gameViewStr.Insert(gameViewStr.IndexOf("local viewClass = _ENV[name]"),
            "if   _ENV[name] == nil then\n                require(_LAZY_REQUIRE[name])\n            end\n");
        File.WriteAllText(gameViewPath,gameViewStr);

        //entity menu
        var requireEntityMenus = entityMenus.Where((@class => @class.ClassName != "entity_menu_panel")).Select(input =>
        {
            var singleclass = input.singleFileMultiClass;
            return $"""
                            {input.ClassName} = "{input.RequirePath}"
                    """;
        }).ToList();
        var requireEntityMenuTable = $$"""
                               
                                   local _LAZY_REQUIRE = {
                                   {{string.Join(",\n", requireEntityMenus)}}
                                   }

                               """;
        
        FINDSTR = "inherit \"ViewBase\"";
        var entityMenuPanelPath = Path.Combine(Const.toTopLuaDir, "GameView/EntityMenu/core/entity_menu_panel.lua");
        var entityMenuPaneStr = File.ReadAllText(entityMenuPanelPath);
        indexOf = entityMenuPaneStr.IndexOf(FINDSTR) + FINDSTR.Length;
        entityMenuPaneStr = entityMenuPaneStr.Insert(indexOf, requireEntityMenuTable);
        entityMenuPaneStr = entityMenuPaneStr.Insert(entityMenuPaneStr.IndexOf("local __creator = _ENV[lua_name];"),
            "if   _ENV[lua_name] == nil then\n                require(_LAZY_REQUIRE[lua_name])\n            end\n");
        File.WriteAllText(entityMenuPanelPath,entityMenuPaneStr);
        
        //msg items
        var requireMsgItems = msgItems.Where((@class => @class.ClassName != "")).Select(input =>
        {
            var singleclass = input.singleFileMultiClass;
            return $"""
                            {input.ClassName} = "{input.RequirePath}"
                    """;
        }).ToList();
        var requireMsgItemsTable = $$"""
                                     
                                         local _LAZY_REQUIRE = {
                                         {{string.Join(",\n", requireMsgItems)}}
                                         }

                                     """;
        
        FINDSTR = "inherit \"ViewBase\"";
        var msgPanelPath = Path.Combine(Const.toTopLuaDir, "GameView/Msg/msg_panel.lua");
        var msgPaneStr = File.ReadAllText(msgPanelPath);
        indexOf = msgPaneStr.IndexOf(FINDSTR) + FINDSTR.Length;
        msgPaneStr = msgPaneStr.Insert(indexOf, requireMsgItemsTable);
        msgPaneStr = msgPaneStr.Insert(msgPaneStr.IndexOf("local itemClass = itemClassName and _ENV[itemClassName]"),
            "if   _ENV[itemClassName] == nil then\n                require(_LAZY_REQUIRE[itemClassName])\n            end\n");
        File.WriteAllText(msgPanelPath,msgPaneStr);

        //处理confCondition

        var cfgConditionTables = cfgConditionClass.Where((@class => true)).Select((input) =>
        {
            var conditionTypeStr = string.Empty;   
            foreach (var statement in input.Statements)
            {
                if (statement is Assignment assignment)
                {
                    if (assignment.Targets[0] is Variable variable && variable.Name == "__ctor")
                    {
                        if (assignment.Values[0] is FunctionDefinition functionDefinition)
                        {
                            foreach (var statement1 in functionDefinition.Block.Statements)
                            {
                                if (statement1 is Assignment assignment1)
                                {
                                    // self.__condition_type = ConditionType.DIG_SPEED_COUNT
                                    if (assignment1.Targets[0] is TableAccess tableAccess && 
                                        tableAccess.Table is Variable variable1 && variable1.Name == "self" && 
                                        tableAccess.Index is StringLiteral index1 && index1.Value == "__condition_type")
                                    {
                                        conditionTypeStr = assignment1.Values[0].ToString();
                                    }
                                }
                            }
                        }

                        break;
                    }
                }
            }
            Debug.Assert(conditionTypeStr.Length != 0);
            return $$"""
                            [{{conditionTypeStr}}] = {
                                ClassName = "{{input.ClassName}}",
                                RequirePath = "{{input.RequirePath}}",
                            }
                    """;
        });
        var requireCfgConditionTable = $$"""
                                     
                                         local _LAZY_REQUIRE = {
                                         {{string.Join(",\n", cfgConditionTables)}}
                                         }

                                     """;
       // Console.WriteLine(requireCfgConditionTable);
       var cfgConditionBindPath = Path.Combine(Const.toTopLuaDir, "CommonExt/Logic/CfgCondition/CfgConditionBind.lua");
       var cfgConditionBindStr = File.ReadAllText(cfgConditionBindPath);
       cfgConditionBindStr = cfgConditionBindStr.Insert(cfgConditionBindStr.IndexOf("class \"CfgCondition\"(function(_ENV)"),requireCfgConditionTable);

       FINDSTR = "inherit \"LuaObject\"";
       cfgConditionBindStr = cfgConditionBindStr.Insert(cfgConditionBindStr.IndexOf(FINDSTR)+FINDSTR.Length, """
                                                                             
                                                                             __Indexer__()
                                                                             property "Condition_type_class" { field = "__Condition_type_class", type = Table, default = {},
                                                                                                get = function(self, conditionType)
                                                                                                     if self.__Condition_type_class[conditionType] then
                                                                                                         return self.__Condition_type_class[conditionType]
                                                                                                     end
                                                                                                    local LazyRequire = _LAZY_REQUIRE[conditionType]
                                                                                                    if(_ENV[LazyRequire.ClassName] == nil) then
                                                                                                        require(LazyRequire.RequirePath)
                                                                                                    end
                                                                                                     local classValue = _ENV[LazyRequire.ClassName]
                                                                                                    local value = classValue();
                                                                                                    if value or value.ConditionType ~= "BaseType" then
                                                                                                        self.__Condition_type_class[value.ConditionType] = value;
                                                                                                    else
                                                                                                        logError("create conditionClass error!! name:" .. classValue);
                                                                                                    end
                                                                                                    return value
                                                                                                end,
                                                                                                set = false, -- 外部不允许修改
                                                                             }
                                                                             
                                                                             """);




       var splits = cfgConditionBindStr.Split("\r\n").ToList();
       var _index = 0;
       for (var i = 0; i < splits.Count; i++)
       {
           if (splits[i].Contains("local value;"))
           {
               _index = i;
               break;
           }
       }

       splits.Insert(_index, "--[[");
       splits.Insert(_index+10, "--]]");
       cfgConditionBindStr = string.Join("\r\n", splits);
       File.WriteAllText(cfgConditionBindPath,cfgConditionBindStr);

       
       var cfgConditionPath = Path.Combine(Const.toTopLuaDir, "Common/Logic/CfgCondition/core/CfgCondition.lua");
       string cfgConditionstr = File.ReadAllText(cfgConditionPath);
       cfgConditionstr = cfgConditionstr.Replace("local condition_class = self.__Condition_type_class[conditionType]", "local condition_class = self.Condition_type_class[conditionType]");
       File.WriteAllText(cfgConditionPath,cfgConditionstr);

        // var entityMenuAssignment = new Assignment()
        // {
        //     IsLocal = true,
        //     Targets = new List<IAssignable>()
        //     {
        //         new Variable() { Name = "_ENV" }
        //     },
        //     Values = new List<IExpression>()
        //     {
        //         new TableConstructor()
        //         {
        //             Entries = entityMenus.Where((@class => @class.ClassName != "entity_menu_panel")).Select(input =>
        //             {
        //                 var singleclass = input.singleFileMultiClass;
        //                 return new TableConstructor.Entry()
        //                 {
        //                     ExplicitKey = true,
        //                     Key = new StringLiteral() { Value = input.ClassName },
        //                     Value = singleclass == false
        //                                         ? new FunctionCall()
        //                                         {
        //                                             Arguments = new List<IExpression>()
        //                                             {
        //                                                 new StringLiteral()
        //                                                 {
        //                                                     Value = input.RequirePath
        //                                                 }
        //                                             },
        //                                             Function = new Variable()
        //                                             {
        //                                                 Name = "require",
        //                                             }
        //                                         }
        //                                         : new TableAccess()
        //                                         {
        //                                             Table = new FunctionCall()
        //                                             {
        //                                                 Arguments = new List<IExpression>()
        //                                                 {
        //                                                     new StringLiteral()
        //                                                     {
        //                                                         Value = input.RequirePath
        //                                                     }
        //                                                 },
        //                                                 Function = new Variable()
        //                                                 {
        //                                                     Name = "require",
        //                                                 }
        //                                             },
        //                                             Index = new StringLiteral()
        //                                             {
        //                                                 Value = input.ClassName
        //                                             }
        //                                         }
        //                         
        //                 };
        //             }).ToList()
        //         }
        //     }
        // };
        //
        //
        //
        // find = false;
        // foreach (var moduleAndClass in moduleAndClasses)
        // {
        //     foreach (var ploopClass in moduleAndClass.Classes)
        //     {
        //         if (ploopClass.ClassName == "entity_menu_panel")
        //         {
        //             ploopClass.Statements.Insert(0, entityMenuAssignment);
        //             find = true;
        //             break;
        //         }
        //     }
        //     if(find)
        //         break;
        // }
        // Console.WriteLine(uiAssignment);
        // foreach (var ui in uis)
        // {
        //     Console.WriteLine(ui.ClassName);
        // }


        // var entityButtonDataAssignment = new Assignment()
        // {
        //     IsLocal = true,
        //     Targets = new List<IAssignable>()
        //     {
        //         new Variable() { Name = "_ENV" }
        //     },
        //     Values = new List<IExpression>()
        //     {
        //         new TableConstructor()
        //         {
        //             Entries = entityBtnDatas.Where((@class => @class.ClassName != "")).Select(input =>
        //             {
        //                 var singleclass = input.singleFileMultiClass;
        //                 return new TableConstructor.Entry()
        //                 {
        //                     ExplicitKey = true,
        //                     Key = new StringLiteral() { Value = input.ClassName },
        //                     Value = singleclass == false
        //                                         ? new FunctionCall()
        //                                         {
        //                                             Arguments = new List<IExpression>()
        //                                             {
        //                                                 new StringLiteral()
        //                                                 {
        //                                                     Value = input.RequirePath
        //                                                 }
        //                                             },
        //                                             Function = new Variable()
        //                                             {
        //                                                 Name = "require",
        //                                             }
        //                                         }
        //                                         : new TableAccess()
        //                                         {
        //                                             Table = new FunctionCall()
        //                                             {
        //                                                 Arguments = new List<IExpression>()
        //                                                 {
        //                                                     new StringLiteral()
        //                                                     {
        //                                                         Value = input.RequirePath
        //                                                     }
        //                                                 },
        //                                                 Function = new Variable()
        //                                                 {
        //                                                     Name = "require",
        //                                                 }
        //                                             },
        //                                             Index = new StringLiteral()
        //                                             {
        //                                                 Value = input.ClassName
        //                                             }
        //                                         }
        //                         
        //                 };
        //             }).ToList()
        //         }
        //     }
        // };
        //
        // find = false;
        // foreach (var moduleAndClass in moduleAndClasses)
        // {
        //     foreach (var ploopClass in moduleAndClass.Classes)
        //     {
        //         if (ploopClass.ClassName == "EntityMenuModule" && ploopClass.IsMainPartialClass)
        //         {
        //             ploopClass.Statements.Insert(0, entityButtonDataAssignment);
        //             find = true;
        //             break;
        //         }
        //     }
        //     if(find)
        //         break;
        // }
        // Console.WriteLine(entityButtonDataAssignment);




        // var condJudgeExpAssignment = new Assignment()
        // {
        //     IsLocal = true,
        //     Targets = new List<IAssignable>()
        //     {
        //         new Variable() { Name = "_ENV" }
        //     },
        //     Values = new List<IExpression>()
        //     {
        //         new TableConstructor()
        //         {
        //             Entries = condJudgeExps.Where((@class => @class.ClassName != "")).Select(input =>
        //             {
        //                 var singleclass = input.singleFileMultiClass;
        //                 return new TableConstructor.Entry()
        //                 {
        //                     ExplicitKey = true,
        //                     Key = new StringLiteral() { Value = input.ClassName },
        //                     Value = singleclass == false
        //                                         ? new FunctionCall()
        //                                         {
        //                                             Arguments = new List<IExpression>()
        //                                             {
        //                                                 new StringLiteral()
        //                                                 {
        //                                                     Value = input.RequirePath
        //                                                 }
        //                                             },
        //                                             Function = new Variable()
        //                                             {
        //                                                 Name = "require",
        //                                             }
        //                                         }
        //                                         : new TableAccess()
        //                                         {
        //                                             Table = new FunctionCall()
        //                                             {
        //                                                 Arguments = new List<IExpression>()
        //                                                 {
        //                                                     new StringLiteral()
        //                                                     {
        //                                                         Value = input.RequirePath
        //                                                     }
        //                                                 },
        //                                                 Function = new Variable()
        //                                                 {
        //                                                     Name = "require",
        //                                                 }
        //                                             },
        //                                             Index = new StringLiteral()
        //                                             {
        //                                                 Value = input.ClassName
        //                                             }
        //                                         }
        //                         
        //                 };
        //             }).ToList()
        //         }
        //     }
        // };
        //
        // find = false;
        // foreach (var moduleAndClass in moduleAndClasses)
        // {
        //     foreach (var ploopClass in moduleAndClass.Classes)
        //     {
        //         if (ploopClass.ClassName == "CondJudgment")
        //         {
        //             ploopClass.Statements.Insert(0, condJudgeExpAssignment);
        //             find = true;
        //             break;
        //         }
        //     }
        //     if(find)
        //         break;
        // }
        // Console.WriteLine(condJudgeExpAssignment);

    }
}