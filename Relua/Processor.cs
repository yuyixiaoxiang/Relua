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
                file.Block = expr;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        //post process
        PostprocessPartialClass();
        PostprocessProperties();
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

    /// <summary>
    /// post ploop properties
    /// </summary>
    private void PostprocessProperties()
    {
        foreach (var file in this.files)
        {
            PloopModule? module = file.Block.Statements.Find((statement => statement is PloopModule)) as PloopModule;
            List<IStatement> allclasses = file.Block.Statements.FindAll((statement => statement is PloopClass));
            var class__ = module?.Statements.FindAll((statement => statement is PloopClass));
            if (class__ != null)
                allclasses.AddRange(class__);
            foreach (var class_ in allclasses)
            {
                if (class_ is PloopClass _ploopClass)
                {
                    PloopClass mainCtorClass = default;
                    //find the default ploop constructor 
                    if (_ploopClass.IsMainPartialClass || !_ploopClass.IsPartialClass)
                        mainCtorClass = _ploopClass;
                    else
                        mainCtorClass = _ploopClass.MainPartialClass;
                    Debug.Assert(mainCtorClass != null, nameof(mainCtorClass) + " != null");
                    
                    var _ctor = mainCtorClass.Statements.Find((statement=>
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
                                assignment.Values.Exists(assignable =>
                                {
                                    return assignable is FunctionDefinition;
                                });
                        }
                        return false;
                    }));
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
                    foreach (var property in properties)
                    {
                        if (property is PloopProperty _ploopProperty)
                        {
                            var fieldAssignment = _ploopProperty.GetFieldAssignment();
                            if (fieldAssignment != null)
                            {
                                ctorDefinition.Block.Statements.Insert(_index++,fieldAssignment);
                            }
                        }
                    }
                }
            }
        }
    }

    private void GetStateMents(IStatement statement)
    {
    }

    public class PartialClass
    {
        public class InternalClass
        {
            public LuaArtifact file;
            public PloopClass PloopClass;
        }

        public string className;
        public List<InternalClass> internalClasses = new List<InternalClass>();
    }

    private void PostprocessPartialClass()
    {
        //process partial class
        var tmpDic = new Dictionary<string, PartialClass>();
        foreach (var file in files)
        {
            Debug.Assert(file.Block != null, nameof(file.Block) + " != null");
            PloopModule? module = file.Block.Statements.Find((statement => statement is PloopModule)) as PloopModule;
            PloopClass? class_ = file.Block.Statements.Find((statement => statement is PloopClass)) as PloopClass;
            var key = string.Empty;
            if (module != null && module is PloopModule _ploopModule)
            {
                key = _ploopModule.ModuleName;
                class_ = _ploopModule.Statements.Find((statement => statement is PloopClass)) as PloopClass;
            }

            if (class_ != null && class_ is PloopClass _class)
            {
                key += "." + _class.ClassName;
            }

            if (class_ != null)
            {
                // fulfile the require path 
                class_.RequirePath = file.requirePath;
                var tmpClass = new PartialClass.InternalClass()
                {
                    file = file,
                    PloopClass = class_,
                };
                if (tmpDic.ContainsKey(key))
                {
                    tmpDic[key].internalClasses.Add(tmpClass);
                }
                else
                {
                    tmpDic.Add(key, new PartialClass
                    {
                        className = class_.ClassName,
                        internalClasses = new List<PartialClass.InternalClass>()
                        {
                            tmpClass
                        },
                    });
                }
            }
        }

        foreach (var kv in tmpDic)
        {
            if (kv.Value.internalClasses.Count > 1)
            {
                try
                {
                    var mainPartialClass =
                        kv.Value.internalClasses.Find((@class => @class.PloopClass.ClassName == @class.file.fileName));
                    var subPartialClass =
                        kv.Value.internalClasses.FindAll(
                            (@class => @class.PloopClass.ClassName != @class.file.fileName));
                    mainPartialClass.PloopClass.IsPartialClass = true;
                    mainPartialClass.PloopClass.IsMainPartialClass = true;

                    foreach (var internalClass in subPartialClass)
                    {
                        internalClass.PloopClass.IsPartialClass = true;
                        internalClass.PloopClass.MainPartialRequirePath = mainPartialClass.PloopClass.RequirePath;
                        internalClass.PloopClass.MainPartialClass = mainPartialClass.PloopClass;
                        mainPartialClass.PloopClass.SubPartialRequirePaths.Add(internalClass.PloopClass.RequirePath);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{kv.Key} \n{kv.Value.className} \n{e}");
                }
            }
        }
    }
}