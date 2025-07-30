using System.Diagnostics;
using Lua.AST;

namespace Lua;

public class Processor
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
    /// process all lua files to ast
    /// </summary>
    /// <returns></returns>
    public List<(string path, string content)> Process()
    {
        foreach (var file in files)
        {
            try
            {
                var tokenizer = new Tokenizer(File.ReadAllText(file.srcPath));
                var parser = new Parser(tokenizer);
                var expr = parser.Read();
                expr.ToString();
                var context = new CheckContext();
                expr.CheckNode(context,null);
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
        ProcessSafeRequireFile();
        PostprocessModuleAndPartialClass();
        //todo check auto require 
        
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

    /// <summary>
    /// post process module and class
    /// </summary>
    /// <exception cref="Exception"></exception>
    private void PostprocessModuleAndPartialClass()
    {
        //process partial class
        List<ModuleAndClass> moduleAndClasses = new List<ModuleAndClass>();
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
                Console.WriteLine($"Multi Class File: {file.srcPath}");
                file.Block.Statements.Add(new Return()
                {
                    Expressions = new List<IExpression>()
                    {
                        new TableConstructor()
                        {
                            Entries = moduleAndClasse.Classes.ConvertAll<TableConstructor.Entry>((input =>
                                new TableConstructor.Entry()
                                {
                                    ExplicitKey = true,
                                    Key = new StringLiteral() { Value = input.ClassName },
                                    Value = new Variable() { Name = input.ClassName },
                                }))
                        }
                    }
                });
            }
            else
            {
                if (moduleAndClasse.Classes.Count > 0)
                {
                    file.Block.Statements.Add(new Return()
                    {
                        Expressions = new List<IExpression>()
                        {
                            new Variable(){Name =moduleAndClasse.Classes[0].ClassName }
                        }
                    });    
                }
            }

            moduleAndClasses.Add(moduleAndClasse);
        }
        
        //check the attribute
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                var Statements = _ploopClass.Statements;
                for (var i = 0; i < Statements.Count; i++)
                {
                    var statement = Statements[i];
                    PloopAttribute attribute = null;
                    if (i > 0)
                    {
                        if (Statements[i - 1] is PloopAttribute _ploopAttribute)
                        {
                            attribute = _ploopAttribute;
                        }
                    }

                    if (statement is Assignment assignment)
                    {
                        assignment.Attribute = attribute;
                    }
                    else if (statement is PloopProperty ploopProperty)
                    {
                        ploopProperty.Attribute = attribute;
                    }
                }
            }
        }

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
                if (_class.ClassName == _class.FileName)
                {
                    mainPartialClass = _class;
                }
                else
                {
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
                    if (kv.Key == "MapCityNodeView" && _class.FileName == "CityBuildingView_Scaffold")
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

        //process the super method call
        const string ctor = "__ctor";
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                if (string.IsNullOrEmpty(_ploopClass.InheritClassName) == false)
                {
                    var statements = _ploopClass.Statements;
                    foreach (var statement in statements)
                    {
                        if (statement is Assignment assignment)
                        {
                            var _ctor = false;
                            if (assignment.Targets[0] is Variable variable_)
                            {
                                if (variable_.Name == ctor)
                                    _ctor = true;
                            }

                            foreach (var assignValue in assignment.Values)
                            {
                                if (assignValue is FunctionDefinition functionDefinition)
                                {
                                    foreach (var statement1 in functionDefinition.Block.Statements)
                                    {
                                        if (statement1 is FunctionCall functionCall)
                                        {
                                            if (functionCall.Function is TableAccess tableAccess)
                                            {
                                                if (tableAccess.Table is Variable variable)
                                                {
                                                    if (variable.Name == "super")
                                                    {
                                                        variable.Name = _ploopClass.InheritClassName;
                                                    }
                                                }
                                            }
                                            else if (functionCall.Function is Variable variable2 && _ctor &&
                                                     variable2.Name == "super")
                                            {
                                                functionCall.Function = new TableAccess()
                                                {
                                                    Table = new Variable() { Name = _ploopClass.InheritClassName },
                                                    Index = new StringLiteral() { Value = ctor }
                                                };
                                            }
                                        }
                                    }
                                }
                            }
                        }
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


        //post process property
        foreach (var moduleAndClass in moduleAndClasses)
        {
            foreach (var _ploopClass in moduleAndClass.Classes)
            {
                PloopClass mainCtorClass = default;
                //find the default ploop constructor 
                if (_ploopClass.IsMainPartialClass || !_ploopClass.IsPartialClass)
                    mainCtorClass = _ploopClass;
                else
                    mainCtorClass = _ploopClass.MainPartialClass;
                Debug.Assert(mainCtorClass != null, nameof(mainCtorClass) + " != null");

                var _ctor = mainCtorClass.Statements.Find((statement =>
                {
                    if (statement is Assignment assignment)
                    {
                        return assignment.Targets.Exists(assignable =>
                               {
                                   if (assignable is Variable _variable)
                                   {
                                       if (_variable.Name == "__ctor")
                                           return true;
                                   }

                                   return false;
                               }) &&
                               assignment.Values.Exists(assignable => { return assignable is FunctionDefinition; });
                    }

                    return false;
                }));

                if (_ctor == null)
                {
                    _ctor = new Assignment()
                    {
                        PloopClass = _ploopClass,
                        Targets = new List<IAssignable>()
                        {
                            new Variable()
                            {
                                Name = "__ctor",
                            },
                        },
                        Values = new List<IExpression>()
                        {
                            new FunctionDefinition()
                            {
                                ArgumentNames = new List<string>()
                                {
                                    "self","..."
                                },
                                Block = new Block()
                                {
                                    Statements = new List<IStatement>()
                                    {
                                        string.IsNullOrEmpty(mainCtorClass.InheritClassName) == false?
                                        new FunctionCall()
                                        {
                                            Arguments = new List<IExpression>()
                                            {
                                                new Variable(){Name = "self"},
                                                new VarargsLiteral(),
                                            },
                                            Function = new TableAccess()
                                            {
                                                Table = new Variable() { Name = mainCtorClass.InheritClassName },
                                                Index = new StringLiteral() { Value = "__ctor" }
                                            }
                                        }: new Block()
                                    }
                                },
                                ImplicitSelf = true,
                                PloopClass = _ploopClass,
                            },
                        },
                    };
                    mainCtorClass.Statements.Insert(0, _ctor);
                }

                Debug.Assert(_ctor != null, nameof(_ctor) + " != null");

                Debug.Assert(_ctor is Assignment);

                FunctionDefinition ctorDefinition = null;
                if (_ctor is Assignment _ctorAssignment)
                {
                    ctorDefinition = _ctorAssignment.Values[0] as FunctionDefinition;
                }

                Debug.Assert(ctorDefinition != null, nameof(ctorDefinition) + " != null");

                List<IStatement> properties =
                    _ploopClass.Statements.FindAll((statement => statement is PloopProperty));

                var _index = 0;
                var propertyAssignmentFunctions = new List<IStatement>();
                var localpropertyFieldAssignments = new List<IStatement>();
                foreach (var property in properties)
                {
                    if (property is PloopProperty _ploopProperty)
                    {
                        _ploopProperty.PloopClass = _ploopClass;
                        var fieldAssignment = _ploopProperty.GetPropertyFieldAssignment();
                        if (fieldAssignment != null)
                        {
                            if (!(_ploopProperty.Attribute?.IsStatic ?? false))
                            {
                                ctorDefinition.Block.Statements.Insert(_index++, fieldAssignment);
                            }
                            else
                            {
                                localpropertyFieldAssignments.Add(fieldAssignment);    
                            }
                        }
                        propertyAssignmentFunctions.AddRange(_ploopProperty.GetPropertyFunction());
                    }
                }
                //check
                if (localpropertyFieldAssignments.Count > 0)
                {
                    foreach (var assignment in localpropertyFieldAssignments)
                    {
                        var assignment_ = assignment as Assignment;
                        var fieldName = (assignment_.Targets[0] as Variable).Name;
                        var exists = _ploopClass.Statements.Exists((statement =>
                        {
                            if(statement is Assignment assignment){
                                if (assignment.IsLocal)
                                {
                                    if (assignment.Targets.Exists((assignable =>
                                        {
                                            if (assignable is Variable _variable)
                                            {
                                                if (_variable.Name == fieldName)
                                                {
                                                    return true;
                                                }
                                            }

                                            return false;
                                        })))
                                    {
                                        return true;
                                    }
                                }
                            }
                            return false;
                        }));
                        if (exists == false)
                        {
                            _ploopClass.Statements.Insert(0, assignment);            
                        }
                    }
                }

                //add the assignments get/set
                //检测是否已经有同名的方法,如果有,那么不再处理
                foreach (var assignmentFunction in propertyAssignmentFunctions)
                {
                    if (assignmentFunction is Assignment assignment)
                    {
                        if (assignment.Values[0] is FunctionDefinition definition)
                        {
                            var _name = ((Variable)assignment.Targets[0]).Name;
                            var exists = _ploopClass.Statements.Exists((statement =>
                            {
                                if (statement is Assignment assignment)
                                {
                                    if (assignment.Values[0] is FunctionDefinition _function)
                                    {
                                        if (assignment.Targets[0] is Variable _variable)
                                        {
                                            if (_variable.Name == _name)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                                return false;
                            }));
                            if (exists)
                            {
                                continue;
                            }
                        }
                    }

                    _ploopClass.Statements.Insert(0,assignmentFunction);    
                }
                    
                
                
                //rearrange all the local assignment
                var allLocalAssignments = _ploopClass.Statements.FindAll((statement =>
                {
                    if (statement is Assignment assignment)
                    {
                        if (assignment.IsLocal)
                        {
                            return true;
                        }
                    }

                    return false;
                }));
                _ploopClass.Statements.RemoveAll((statement => allLocalAssignments.Contains(statement)));
                _ploopClass.Statements.InsertRange(0,allLocalAssignments);

                //check GetXXX/SetXXX




            }
        }


        // throw new Exception("custom exception");
    }
}