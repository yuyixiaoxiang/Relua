using System.Diagnostics;
using System.Text;

namespace Lua.AST
{
    /// <summary>
    /// Base class of all Lua AST node.
    /// </summary>
    public abstract class Node : ICheckNode
    {
        private Node _parent;
        private HashSet<Node> _children = new HashSet<Node>();
        public HashSet<Node> children => _children;

        public Node parent
        {
            get { return _parent; }
            set
            {
                _parent = value;
                value?.children.Add(this);
            }
        }

        public virtual void LookupVariable(ICheckContext context, Node child, string variable)
        {
            parent?.LookupVariable(context, this, variable);
        }

        public virtual IList<string> ProvideVariables()
        {
            return Array.Empty<string>();
        }

        public void ExportableVariables(ICheckContext context)
        {
        }

        public abstract void CheckNode(ICheckContext context, Node parent);

        public abstract void Write(IndentAwareTextWriter writer, object data = null);

        public abstract void Write2TS(IndentAwareTextWriter writer, object data = null);

        public override string ToString()
        {
            return ToString(false);
        }

        public virtual string GetTag()
        {
            return GetType().Name + $"({this.GetHashCode()})";
        }

        public virtual string ToString(bool one_line)
        {
            var s = new StringBuilder();
            var sw = new StringWriter(s);
            var iw = new IndentAwareTextWriter(sw);
            iw.ForceOneLine = one_line;
            Write(iw);
            return s.ToString();
        }
    }

    public class IntermediateNode : Node
    {
        protected ICheckNode node;
        protected string TAG;

        public IntermediateNode()
        {
        }

        public IntermediateNode(ICheckNode node, string TAG)
        {
            this.node = node;
            this.TAG = TAG;
        }

        public override string GetTag()
        {
            return this.TAG + $"({this.GetHashCode()})";
        }

        public override void Write(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            node.CheckNode(context, this);
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    public class ExportVariable
    {
        public string VariableName;
        public bool IsField;
        public bool IsClass;
        public bool IsMethod;
        public bool IsClassField;
        public bool IsClassMethod;

        public override string ToString()
        {
            return
                $"{nameof(VariableName)}: {VariableName}, {nameof(IsField)}: {IsField}, {nameof(IsClass)}: {IsClass}, {nameof(IsMethod)}: {IsMethod}, {nameof(IsClassField)}: {IsClassField}, {nameof(IsClassMethod)}: {IsClassMethod}";
        }
    }

    public interface ICheckNode
    {
        void CheckNode(ICheckContext context, Node parent);
        void LookupVariable(ICheckContext context, Node child, string variable);
        IList<string> ProvideVariables();
        void ExportableVariables(ICheckContext context);
    }

    public interface ICheckContext
    {
        void AddRequiredVariable(string variable);
        ICollection<string> GetRequiredVariables();
        void AddExportVariable(ExportVariable exportVariable);
        ICollection<ExportVariable> GetExportableVariables();
    }

    public class CheckContext : ICheckContext
    {
        private HashSet<string> requiredVariables = new HashSet<string>();
        private List<ExportVariable> exportableVariables = new List<ExportVariable>();

        private HashSet<string> PUBLIC_FUNCTIONS = new HashSet<string>()
        {
            "table", "ipairs", "logError", "math", "type", "tonumber", "pairs", "safe", "logWarn", "tostring", "VhLog",
            "logErrorEx", "debug",
            "tostring", "logEx", "log", "protobuf", "showMsg", "string", "VhLogError"
        };

        private HashSet<string> PLOOP_VARIABLES = new HashSet<string>()
        {
            "System", "self",
        };

        private HashSet<string> GLOBALS_VARIABLES = new HashSet<string>()
        {
            "MODULE", "DATA", "EVENT", "NET", "VIEW", "g_Util", "NO_UPDATE", "ACTION", "ELEX_SDK", "Timer", "Time",
            "EPSDK"
        };

        private HashSet<string> OMIT_EXPORTABLE_VARIABLES = new()
        {
            "__ctor", "__dtor"
        };


        public void AddRequiredVariable(string variable)
        {
            if (Tokenizer.RESERVED_KEYWORDS.Contains(variable))
                return;
            if (PUBLIC_FUNCTIONS.Contains(variable))
                return;
            if (PLOOP_VARIABLES.Contains(variable))
                return;
            if (GLOBALS_VARIABLES.Contains(variable))
                return;
            requiredVariables.Add(variable);
        }

        public void AddExportVariable(ExportVariable variable)
        {
            if (OMIT_EXPORTABLE_VARIABLES.Contains(variable.VariableName))
                return;
            exportableVariables.Add(variable);
        }

        public ICollection<string> GetRequiredVariables()
        {
            return requiredVariables;
        }

        public ICollection<ExportVariable> GetExportableVariables()
        {
            return exportableVariables;
        }

        public override string ToString()
        {
            return
                $"-----------------------------------------\nRequireVariables:{string.Join("\n\t", requiredVariables)}" +
                $"\nExportableVariables:{string.Join("\n\t", exportableVariables)}";
        }
    }

    /// <summary>
    /// Interface (used as a sort of "type tag") for Lua AST nodes that are statements.
    /// </summary>
    public interface IStatement : ICheckNode
    {
        void Write(IndentAwareTextWriter writer, object data = null);

        void Write2TS(IndentAwareTextWriter writer, object data = null);
        string ToString(bool one_line);
    }

    /// <summary>
    /// Interface (used as a sort of "type tag") for Lua AST nodes that are expressions.
    /// </summary>
    public interface IExpression : ICheckNode
    {
        void Write(IndentAwareTextWriter writer, object data = null);

        void Write2TS(IndentAwareTextWriter writer, object data = null);
        string ToString(bool one_line);
    }

    /// <summary>
    /// Interface (used as a sort of "type tag") for Lua AST nodes that are "assignable" expressions
    /// (variables and table access).
    /// </summary>
    public interface IAssignable : ICheckNode
    {
        void Write(IndentAwareTextWriter writer, object data);

        void Write2TS(IndentAwareTextWriter writer, object data);
        string ToString(bool one_line);
    }

    /// <summary>
    /// Variable expression.
    /// 
    /// ```
    /// some_var
    /// ```
    /// </summary>
    public class Variable : Node, IExpression, IAssignable
    {
        public string Name;

        public override string GetTag()
        {
            return $"Variable({Name})";
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;

            // StringBuilder sb = new StringBuilder();
            // var path = new List<Node>();
            // Node current = this;
            //
            // // 迭代向上查找，直到根节点
            // while (current != null)
            // {
            //     path.Add(current);
            //     current = current.parent;
            // }
            //
            // // 反转路径，使其从根节点开始
            // path.Reverse();
            // var str = string.Join("-->", path.Select(node =>
            // {
            //     return node.GetTag();
            // }));
            // Console.WriteLine(str);

            LookupVariable(context, this, Name);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write(Name);
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Nil value literal expression.
    /// 
    /// ```
    /// nil
    /// ```
    /// </summary>
    public class NilLiteral : Node, IExpression
    {
        public static NilLiteral Instance = new NilLiteral();

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("nil");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Varargs literal expression. Please note that in the case of
    /// `FunctionDefinition`s parameter names are read as strings, not AST nodes.
    /// This node will only appear in actual uses of the value of the varargs in
    /// statements and expressions, and to detect if a `FunctionDefinition`
    /// accepts varargs, you have to use its `AcceptsVarargs` field.
    /// 
    /// ```
    /// ...
    /// ```
    /// </summary>
    public class VarargsLiteral : Node, IExpression
    {
        public static VarargsLiteral Instance = new VarargsLiteral();

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("...");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Boolean literal expression.
    /// 
    /// ```
    /// true
    /// false
    /// ```
    /// </summary>
    public class BoolLiteral : Node, IExpression
    {
        public static BoolLiteral TrueInstance = new BoolLiteral { Value = true };
        public static BoolLiteral FalseInstance = new BoolLiteral { Value = false };
        public bool Value;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write(Value ? "true" : "false");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Unary operation expression.
    /// 
    /// ```
    /// not value
    /// -value
    /// #table
    /// </summary>
    public class UnaryOp : Node, IExpression
    {
        public enum OpType
        {
            Negate,
            Invert,
            Length
        }

        public OpType Type;
        public IExpression Expression;

        public UnaryOp()
        {
        }

        public UnaryOp(OpType type, IExpression expr)
        {
            Type = type;
            Expression = expr;
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Expression.CheckNode(context, this);
        }

        public static void WriteUnaryOp(OpType type, IndentAwareTextWriter writer)
        {
            switch (type)
            {
                case OpType.Negate: writer.Write("-"); break;
                case OpType.Invert: writer.Write("not "); break;
                case OpType.Length: writer.Write("#"); break;
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("(");
            WriteUnaryOp(Type, writer);
            (Expression as Node).Write(writer);
            writer.Write(")");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Binary operation expression. Please note that the parser uses precedence
    /// rules that favors combining the left side for operations that are
    /// order independent. For example, if parsing an expression like `a + b + c`,
    /// the structure of the AST will *always* look like `((a + b) + c)`, even
    /// though `(a + (b + c))` would yield the same result mathematically (but
    /// not necessarily in Lua if you consider the use of metatables!).
    /// 
    /// ```
    /// a + b
    /// a - b
    /// a * b
    /// a / b
    /// a ^ b
    /// a % b
    /// a .. b
    /// a > b
    /// a >= b
    /// a &lt; b
    /// a &lt;= b
    /// a == b
    /// a ~= b
    /// a and b
    /// a or b
    /// ```
    /// </summary>
    public class BinaryOp : Node, IExpression
    {
        public enum OpType
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Power,
            Modulo,
            Concat,
            GreaterThan,
            GreaterOrEqual,
            LessThan,
            LessOrEqual,
            Equal,
            NotEqual,
            And,
            Or
        }

        public static void WriteBinaryOp(OpType type, IndentAwareTextWriter writer)
        {
            switch (type)
            {
                case OpType.Add: writer.Write("+"); break;
                case OpType.Subtract: writer.Write("-"); break;
                case OpType.Multiply: writer.Write("*"); break;
                case OpType.Divide: writer.Write("/"); break;
                case OpType.Power: writer.Write("^"); break;
                case OpType.Modulo: writer.Write("%"); break;
                case OpType.Concat: writer.Write(".."); break;
                case OpType.GreaterThan: writer.Write(">"); break;
                case OpType.GreaterOrEqual: writer.Write(">="); break;
                case OpType.LessThan: writer.Write("<"); break;
                case OpType.LessOrEqual: writer.Write("<="); break;
                case OpType.Equal: writer.Write("=="); break;
                case OpType.NotEqual: writer.Write("~="); break;
                case OpType.And: writer.Write("and"); break;
                case OpType.Or: writer.Write("or"); break;
            }
        }


        public BinaryOp()
        {
        }

        public BinaryOp(OpType type, IExpression left, IExpression right)
        {
            Type = type;
            Left = left;
            Right = right;
        }

        public OpType Type;
        public IExpression Left;
        public IExpression Right;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Left.CheckNode(context, this);
            Right.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("(");
            (Left as Node).Write(writer);
            writer.Write(" ");
            WriteBinaryOp(Type, writer);
            writer.Write(" ");
            (Right as Node).Write(writer);
            writer.Write(")");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// String literal expression. Note that `Value` will contain the real value
    /// of the string, with all escape sequences interpreted accordingly. Single
    /// quote string literals are also supported.
    /// 
    /// ```
    /// "Hello, world!"
    /// 'Hello, world!'
    /// ```
    /// </summary>
    public class StringLiteral : Node, IExpression
    {
        public static void Quote(IndentAwareTextWriter s, string str)
        {
            s.Write('"');
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                if (c == '\n') s.Write("\\n");
                else if (c == '\t') s.Write("\\t");
                else if (c == '\r') s.Write("\\r");
                else if (c == '\a') s.Write("\\a");
                else if (c == '\b') s.Write("\\b");
                else if (c == '\f') s.Write("\\f");
                else if (c == '\v') s.Write("\\v");
                else if (c == '\\') s.Write("\\\\");
                else if (c == '"') s.Write("\\\"");
                else if (!c.IsASCIIPrintable()) s.Write($"\\{((int)c).ToString("D3")}");
                else s.Write(c);
            }

            s.Write('"');
        }

        public string Value;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            Quote(writer, Value);
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Number literal expression. The value is always a `double`. If `HexFormat`
    /// is true, the value will be converted to a long and then written in hex.
    /// 
    /// ```
    /// 123
    /// 0xFF
    /// ```
    /// </summary>
    public class NumberLiteral : Node, IExpression
    {
        public double Value;
        public bool HexFormat = false;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (HexFormat)
            {
                writer.Write("0x");
                writer.Write(((long)Value).ToString("X"));
            }
            else writer.Write(Value);
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Long literal expression (LuaJIT style). If `HexFormat` is true, the
    /// value will always be written in hex. Will never be read if the
    /// `EnableLuaJITLongs` option of `Parser.Settings` is `false` (it's `true`
    /// by default).
    /// 
    /// ```
    /// 123LL
    /// 0x123LL
    /// ```
    /// </summary>
    public class LuaJITLongLiteral : Node, IExpression
    {
        public long Value;
        public bool HexFormat = false;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (HexFormat)
            {
                writer.Write("0x");
                writer.Write(Value.ToString("X4"));
            }
            else writer.Write(Value);

            writer.Write("LL");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Table access assignable expression. Note that this can be used with any
    /// Lua type and not just  tables due to metatables. When writing, an
    /// attempt will be made to use syntax sugar if possible - assuming that
    /// the index is a literal string which is a valid identifier, dot-style
    /// access will be used as opposed to the verbose bracket syntax
    /// (i.e. `a.b` instead of `a["b"]`).
    /// 
    /// ```
    /// table.val
    /// ("abc").x
    /// _G["abc"]
    /// ```
    /// </summary>
    public class TableAccess : Node, IExpression, IAssignable
    {
        public IExpression Table;
        public IExpression Index;

        private bool GetIdentifierAccessChain(StringBuilder s, bool is_method_access_top_level = false)
        {
            if (Table is TableAccess)
            {
                if (!((TableAccess)Table).GetIdentifierAccessChain(s)) return false;
            }
            else if (Table is Variable)
            {
                s.Append(((Variable)Table).Name);
            }
            else return false;

            if (is_method_access_top_level)
            {
                s.Append(":");
            }
            else s.Append(".");

            if (Index is StringLiteral)
            {
                var lit = (StringLiteral)Index;
                if (!lit.Value.IsIdentifier()) return false;

                s.Append(lit.Value);
            }
            else return false;

            return true;
        }

        public string GetIdentifierAccessChain(bool is_method_access)
        {
            var s = new StringBuilder();
            if (!GetIdentifierAccessChain(s, is_method_access)) return null;
            return s.ToString();
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            new IntermediateNode(Table, "TableAccessTable").CheckNode(context, this);
            new IntermediateNode(Index, "TableAccessIndex").CheckNode(context, this);
        }

        public void WriteDotStyle(IndentAwareTextWriter writer, string index)
        {
            if (Table is StringLiteral) writer.Write("(");
            Table.Write(writer);
            if (Table is StringLiteral) writer.Write(")");
            writer.Write(".");
            writer.Write(index);
        }

        public void WriteGenericStyle(IndentAwareTextWriter writer)
        {
            if (Table is StringLiteral) writer.Write("(");
            Table.Write(writer);
            if (Table is StringLiteral) writer.Write(")");
            writer.Write("[");
            Index.Write(writer);
            writer.Write("]");
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (Index is StringLiteral && ((StringLiteral)Index).Value.IsIdentifier())
            {
                WriteDotStyle(writer, ((StringLiteral)Index).Value);
            }
            else
            {
                WriteGenericStyle(writer);
            }
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Function call expression and statement.
    /// 
    /// If `ForceTruncateReturnValues` is `true`, then this call will be
    /// surrounded with parentheses to make sure that the result of the
    /// expression is only its first return value. `ForceTruncateReturnValues`
    /// will automatically be unset if the `FunctionCall` is augmented with
    /// expressions such as unary operators or table access, because those
    /// operations automatically truncate the function's return values.
    /// 
    /// Method-style function calls will be parsed into this node. The `Function`
    /// field will then be a `TableAccess` node, and the `Arguments` list will
    /// be prepended with the `Table` field of that node.
    /// 
    /// While writing, an attempt will be made to simplify the function call
    /// to a method-style call if possible (`a:b()` syntax). This will only
    /// happen if `Function` is a `TableAccess` node and its index is a string
    /// literal and valid identifier. The `FunctionCall` will also need to have
    /// at least one argument, the first of which must be the `Table` field of
    /// the `TableAccess` node (referential equality). If these conditions are
    /// satisfied, the colon character will be written and the first argument of
    /// the function will be skipped.
    /// 
    /// ```
    /// a("hi")
    /// (function(a) print(a) end)(1, 2, 3)
    /// print"Hello, world!"
    /// do_smth_with_table{shorthand = "syntax"}
    /// obj:method("Hello!")
    /// </summary>
    public class FunctionCall : Node, IExpression, IStatement
    {
        public IExpression Function;
        public List<IExpression> Arguments = new List<IExpression>();
        public bool ForceTruncateReturnValues = false;


        public override void CheckNode(ICheckContext context, Node parent)
        {
            //CUSTOM
            // if (Function is TableAccess tableAccess)
            // {
            //     if (tableAccess.Table is Variable variable && tableAccess.Index is StringLiteral stringLiteral)
            //     {
            //         if (variable.Name == "NetPBCManager" && stringLiteral.Value == "RegisterReceiveMsg")
            //         {
            //             if (Arguments[3] is TableAccess access)
            //             {
            //                 var selfVariable = new Variable() { Name = "self" };
            //                 Arguments[3] = new FunctionDefinition()
            //                 {
            //                     AcceptsVarargs =  true,
            //                     ArgumentNames =  new List<string>(){"..."},
            //                     Block = new Block()
            //                     {
            //                         Statements = new List<IStatement>()
            //                         {
            //                             new FunctionCall()
            //                             {
            //                                 Arguments = new List<IExpression>()
            //                                 {
            //                                     selfVariable,
            //                                     new VarargsLiteral(),
            //                                 },
            //                                 Function = new TableAccess()
            //                                 {
            //                                     Table = selfVariable,
            //                                     Index = access.Index
            //                                 }
            //                             },
            //                         }
            //                     }
            //                 };
            //             }
            //         }
            //     }
            // }


            this.parent = parent;
            foreach (var argument in Arguments)
            {
                new IntermediateNode(argument, "FunctionCallArg").CheckNode(context, this);
            }

            new IntermediateNode(Function, "FunctionCallFuncName").CheckNode(context, this);
        }

        public void WriteMethodStyle(IndentAwareTextWriter writer, IExpression obj, string method_name)
        {
            if (obj is FunctionDefinition) writer.Write("(");
            obj.Write(writer);
            if (obj is FunctionDefinition) writer.Write(")");
            writer.Write(":");
            writer.Write(method_name);
            writer.Write("(");
            for (var i = 1; i < Arguments.Count; i++)
            {
                Arguments[i].Write(writer);
                if (i < Arguments.Count - 1) writer.Write(", ");
            }

            writer.Write(")");
        }

        public void WriteGenericStyle(IndentAwareTextWriter writer)
        {
            if (Function is FunctionDefinition) writer.Write("(");
            Function.Write(writer);
            if (Function is FunctionDefinition) writer.Write(")");
            writer.Write("(");
            for (var i = 0; i < Arguments.Count; i++)
            {
                Arguments[i].Write(writer);
                if (i < Arguments.Count - 1) writer.Write(", ");
            }

            writer.Write(")");
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (ForceTruncateReturnValues) writer.Write("(");

            if (Function is TableAccess && Arguments.Count > 0)
            {
                var tf = ((TableAccess)Function);

                if (tf.Table == Arguments[0] && tf.Index is StringLiteral &&
                    ((StringLiteral)tf.Index).Value.IsIdentifier())
                {
                    WriteMethodStyle(writer, tf.Table, ((StringLiteral)tf.Index).Value);
                }
                else WriteGenericStyle(writer);
            }
            else WriteGenericStyle(writer);

            if (ForceTruncateReturnValues) writer.Write(")");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Table constructor expression. Supports all forms of specifying table
    /// entries, including expression keys (`[expr] = value`), identifier keys
    /// (`id = value`) and sequential keys (`value`).
    /// 
    /// If the `AutofillSequentialKeysInTableConstructor` option
    /// in `Parser.Settings` is set to `true` (which it is by default),
    /// then sequential keys will automatically receive key fields of type
    /// `NumericLiteral` with the appropriate indices. For example, the table
    /// `{3, 2, 1}` will parse as three entries, all with `NumericLiteral` keys
    /// from 1 to 3.
    /// 
    /// Please note that Lua is perfectly fine with repeated keys in table
    /// constructors. Sequential keys can also be used anywhere, even after
    /// indexed keys, therefore the table `{1, 2, [3] = 4, 3}` will produce a
    /// table of length 3 with the values 1, 2, and 3.
    /// 
    /// ```
    /// {
    ///     a = 1,
    ///     b = 2,
    ///     3,
    ///     "x",
    ///     [1 + 3] = true
    /// }
    /// ```
    /// </summary>
    public class TableConstructor : Node, IExpression
    {
        /// <summary>
        /// Table constructor entry. If `ExplicitKey` is `true`, then the key
        /// for this `Entry` will always be emitted while writing the
        /// `TableConstructor`, even if it is a sequential table entry.
        /// 
        /// ```
        /// [3] = "value" -- index is NumberLiteral 3
        /// a = "value" -- index is StringLiteral a
        /// "value" -- index is null or sequential NumberLiteral depending on parser settings
        /// ```
        /// </summary>
        public class Entry : Node
        {
            public IExpression Key;
            public IExpression Value;
            public bool ExplicitKey;

            public override void CheckNode(ICheckContext context, Node parent)
            {
                this.parent = parent;
                Key.CheckNode(context, this);
                Value.CheckNode(context, this);
            }

            public void WriteIdentifierStyle(IndentAwareTextWriter writer, string index)
            {
                writer.Write(index);
                writer.Write(" = ");
                Value.Write(writer);
            }

            public void WriteGenericStyle(IndentAwareTextWriter writer)
            {
                writer.Write("[");
                Key.Write(writer);
                writer.Write("]");
                writer.Write(" = ");
                Value.Write(writer);
            }

            public override void Write(IndentAwareTextWriter writer, object data)
            {
                Write(writer, false);
            }

            public void Write(IndentAwareTextWriter writer, bool skip_key)
            {
                if (skip_key || Key == null)
                {
                    Value.Write(writer);
                    return;
                }

                if (Key is StringLiteral && ((StringLiteral)Key).Value.IsIdentifier())
                {
                    WriteIdentifierStyle(writer, ((StringLiteral)Key).Value);
                }
                else
                {
                    WriteGenericStyle(writer);
                }
            }

            public override void Write2TS(IndentAwareTextWriter writer, object data = null)
            {
                throw new NotImplementedException();
            }
        }

        public List<Entry> Entries = new List<Entry>();

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var entry in Entries)
            {
                entry.CheckNode(context, this);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (Entries.Count == 0)
            {
                writer.Write("{}");
                return;
            }

            if (Entries.Count == 1)
            {
                writer.Write("{ ");
                var ent = Entries[0];
                ent.Write(writer,
                    skip_key: ent.Key is NumberLiteral && ((NumberLiteral)ent.Key).Value == 1 && !ent.ExplicitKey);
                writer.Write(" }");
                return;
            }

            var seq_idx = 1;

            writer.Write("{");
            writer.IncreaseIndent();
            for (var i = 0; i < Entries.Count; i++)
            {
                writer.WriteLine();

                var ent = Entries[i];

                var is_sequential = false;
                if (ent.Key is NumberLiteral && ((NumberLiteral)ent.Key).Value == seq_idx && !ent.ExplicitKey)
                {
                    is_sequential = true;
                    seq_idx += 1;
                }

                Entries[i].Write(writer, skip_key: is_sequential);
                if (i < Entries.Count - 1) writer.Write(",");
            }

            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("}");
            // writer.WriteLine();
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Break statement.
    /// 
    /// ```
    /// break
    /// ```
    /// </summary>
    public class Break : Node, IStatement
    {
        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("break");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Return statement.
    /// 
    /// ```
    /// return
    /// ```
    /// </summary>
    public class Return : Node, IStatement
    {
        public List<IExpression> Expressions = new List<IExpression>();

        /// <summary>
        /// redundant return ,so skip it
        /// </summary>
        public bool Redundant;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var expression in Expressions)
            {
                expression.CheckNode(context, this);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (Redundant)
            {
                writer.Write("--return");
                return;
            }

            writer.Write("return");
            if (Expressions.Count > 0) writer.Write(" ");
            for (var i = 0; i < Expressions.Count; i++)
            {
                var expr = Expressions[i];

                expr.Write(writer);
                if (i < Expressions.Count - 1) writer.Write(", ");
            }
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A block statement. Usually used as part of another statement and not
    /// a standalone statement by itself. If `TopLevel` is true, then the `do
    /// end` construct will never be emitted, even if the node is not being
    /// written by another node.
    /// 
    /// ```
    /// do
    ///     print("abc")
    /// end
    /// </summary>
    public class Block : Node, IStatement
    {
        public List<IStatement> Statements = new List<IStatement>();
        public bool TopLevel;

        public bool IsEmpty => Statements.Count == 0;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var statement in Statements)
            {
                statement.CheckNode(context, this);
            }
        }

        public override void LookupVariable(ICheckContext context, Node child, string variable)
        {
            var indexOf = Statements.IndexOf((IStatement)child);
            if (indexOf > 0)
            {
                for (var i = indexOf - 1; i >= 0; i--)
                {
                    var provides = Statements[i].ProvideVariables();
                    if (provides.Contains(variable))
                    {
                        // Console.WriteLine($"find variable {variable}");
                        return;
                    }
                }
            }

            if (parent == null)
            {
                context.AddRequiredVariable(variable);
                // Console.WriteLine($"not find variable {variable},node:{child.GetTag()}");
                return;
            }

            base.LookupVariable(context, child, variable);
        }

        public void ExportableVariables(ICheckContext context)
        {
            foreach (var statement in Statements)
            {
                statement.ExportableVariables(context);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            Write(writer, true);
        }

        public void Write(IndentAwareTextWriter writer, bool alone)
        {
            if (TopLevel && alone) alone = false;

            if (alone)
            {
                writer.Write("do");
                writer.IncreaseIndent();
                writer.WriteLine();
            }

            for (var i = 0; i < Statements.Count; i++)
            {
                var stat = Statements[i];
                if(stat == null)
                    continue;
                stat.Write(writer);
                //if (writer.ForceOneLine && stat.AmbiguousTermination && i != Statements.Count - 1) {
                //    writer.Write(";");
                //}
                if (i < Statements.Count - 1) writer.WriteLine();
            }

            if (alone)
            {
                writer.DecreaseIndent();
                writer.WriteLine();
                writer.Write("end");
            }
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Conditional block node. This is neither a standalone statement nor an
    /// expression, but a representation of a single generic `if` condition
    /// (i.e. `if` and `elseif`). See `If` for the actual `if` statement.
    /// </summary>
    public class ConditionalBlock : Node
    {
        public IExpression Condition;
        public Block Block;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Condition.CheckNode(context, this);
            Block.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data = null)
        {
            writer.Write("if ");
            Condition.Write(writer);
            writer.Write(" then");
            writer.IncreaseIndent();
            writer.WriteLine();
            Block.Write(writer, false);
            writer.DecreaseIndent();
            writer.WriteLine();
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// If statement.
    /// 
    /// ```
    /// if true then
    ///     print("true")
    /// elseif false then
    ///     print("false")
    /// else
    ///     print("tralse")
    /// end
    /// </summary>
    public class If : Node, IStatement
    {
        public ConditionalBlock MainIf;
        public List<ConditionalBlock> ElseIfs = new List<ConditionalBlock>();
        public Block Else;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            MainIf?.CheckNode(context, this);
            foreach (var elseIf in ElseIfs)
            {
                elseIf.CheckNode(context, this);
            }

            Else?.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            MainIf.Write(writer);
            for (var i = 0; i < ElseIfs.Count; i++)
            {
                writer.Write("else");
                ElseIfs[i].Write(writer);
            }

            if (Else != null)
            {
                writer.Write("else");
                writer.IncreaseIndent();
                writer.WriteLine();
                Else.Write(writer, false);
                writer.DecreaseIndent();
                writer.WriteLine();
            }

            writer.Write("end");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// While statement.
    /// 
    /// ```
    /// while true do
    ///     print("spam")
    /// end
    /// ```
    /// </summary>
    public class While : Node, IStatement
    {
        public IExpression Condition;
        public Block Block;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Condition.CheckNode(context, this);
            Block.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("while ");
            Condition.Write(writer);
            writer.Write(" do");
            writer.IncreaseIndent();
            writer.WriteLine();
            Block.Write(writer, false);
            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("end");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Repeat statement.
    /// 
    /// ```
    /// repeat
    ///     print("TEST")
    /// until test_finished()
    /// ```
    /// </summary>
    public class Repeat : Node, IStatement
    {
        public IExpression Condition;
        public Block Block;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Condition.CheckNode(context, this);
            Block.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("repeat");
            writer.IncreaseIndent();
            writer.WriteLine();
            Block.Write(writer, false);
            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("until ");
            Condition.Write(writer);
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Function definition expression and statement. Do note that parameter
    /// names are stored as strings, since they are not actual expressions.
    /// 
    /// If the function takes variable arguments, the `...` string will not be
    /// included in `ArgumentNames`, but `AcceptsVarargs` will be set to `true`.
    /// 
    /// If the function is empty, it will be shortened to one line (`function()
    /// end`).
    /// 
    /// It is important to note that `FunctionDefinition` is not responsible
    /// for reading or writing functions defined in the named style (e.g.
    /// `function something() ... end`). That operation is interpreted as
    /// an assignment of `FunctionDefinition` to `Variable`, and it is the
    /// `Assignment` node that is responsible both for representing this syntax
    /// and also correctly writing it (avoiding the verbose `something = function()
    /// ... end` if unnecessary).
    /// 
    /// If the function was parsed from the `function a:b() end` syntax, it will
    /// have `ImplicitSelf` set to `true` (and the extra `self` argument). If
    /// this field is `true` while writing a named function Assignment, no
    /// `self` argument will be emitted and the same method definition
    /// syntax will be used. In any other case, a `FunctionDefinition` with
    /// `ImplicitSelf` will emit arguments normally (including the `self`).
    /// 
    /// ```
    /// function() end
    /// function()
    ///     print("a")
    ///     print("b")
    /// end
    /// ```
    /// </summary>
    public class FunctionDefinition : Node, IExpression, IStatement
    {
        public class FunctionDefinitionData
        {
            public PloopClass PloopClass;
            public bool from_named;
            public bool ImplicitSelf;
        }

        public List<string> ArgumentNames = new List<string>();
        public Block Block;
        public bool AcceptsVarargs = false;
        public bool ImplicitSelf = false;
        public PloopClass PloopClass;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Block.CheckNode(context, this);
        }

        public override void LookupVariable(ICheckContext context, Node child, string variable)
        {
            if (ProvideVariables().Contains(variable))
            {
                // Console.WriteLine($"find variable {variable}");
                return;
            }

            base.LookupVariable(context, child, variable);
        }

        public override IList<string> ProvideVariables()
        {
            return ArgumentNames;
        }

        public override void Write(IndentAwareTextWriter writer, object data = null)
        {
            if (data != null && data is FunctionDefinitionData _data)
            {
                PloopClass = _data.PloopClass;
                ImplicitSelf = _data.ImplicitSelf;
                Write(writer, _data.from_named);
            }
            else
            {
                Write(writer, false);
            }
        }

        private void Write(IndentAwareTextWriter writer, bool from_named)
        {
            if (!from_named) writer.Write("function");
            writer.Write("(");

            var arg_start_idx = 0;
            if (ImplicitSelf && from_named) arg_start_idx += 1;
            // Skips the self for method defs

            for (var i = arg_start_idx; i < ArgumentNames.Count; i++)
            {
                var arg = ArgumentNames[i];
                writer.Write(arg);
                if (i < ArgumentNames.Count - 1) writer.Write(", ");
            }

            if (AcceptsVarargs && !ArgumentNames.Contains("..."))
            {
                if (arg_start_idx == 0 ||　ArgumentNames.Count > 1)
                {
                    writer.Write(", ");
                }

                writer.Write("...");
            }

            writer.Write(")");
            if (Block.IsEmpty) writer.Write(" ");
            else
            {
                writer.IncreaseIndent();
                writer.WriteLine();
                Block.Write(writer, false);
                writer.DecreaseIndent();
                writer.WriteLine();
            }

            writer.Write("end");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Assignment statement. Note that Lua allows for multiple elements on
    /// both sides of the statement.
    /// 
    /// This node is responsible not only for representing syntax in the form of
    /// `x = y` and `local x = y`, but also function definitions with names
    /// (`function name() end` or `local function name() end`).
    /// 
    /// If there is only one target, only one value, the value is a `FunctionDefinition`
    /// and the target is a `StringLiteral` that is a valid identifier, then
    /// this node will be written using the named function style as opposed to
    /// assigning an anonymous function to the appropriate variable.
    /// 
    /// If `IsLocal` is `true`, the size of both `Targets` and `Values` is the
    /// same and all entries in `Values` are `NilLiteral`s, a local declaration
    /// will be emitted that in the Lua language implicitly assigns the value
    /// `nil` (i.e. `local a` instead of `local a = nil`). The same syntax will
    /// be used if `IsLocal` is `true` and `Values` has length 0. If
    /// `ForceExplicitLocalNil` is `true`, this will not happen and `nil`s will
    /// always be explicitly assigned.
    /// 
    /// If `ForceExplicitLocalNil` is set to `true`, values emitted will always
    /// match the amount of targets, filling in the missing entries with `nil`s
    /// if necessary.
    /// 
    /// If the parser setting `AutofillValuesInLocalDeclaration` is set to `false`
    /// (it's set to `true` by default), then in the case of a local declaration
    /// like above, the `Values` list will be empty. As mentioned above, the
    /// local declaration will still be written in the fancy syntax, unless the
    /// `ForceEXplicitLocalNil` override is used.
    /// 
    /// ```
    /// x = y
    /// x, y = a, b
    /// a, b, c = (f()), 1, "hi"
    /// function a()
    ///     print("hi")
    /// end
    /// local function b()
    ///     print("hi")
    /// end
    /// ```
    /// </summary>
    public class Assignment : Node, IStatement
    {
        public PloopClass PloopClass;
        public bool IsLocal;
        public bool ForceExplicitLocalNil;
        public PloopAttribute Attribute;
        public List<IAssignable> Targets = new List<IAssignable>();
        public List<IExpression> Values = new List<IExpression>();

        public bool IsFunctionAssignment
        {
            get { return Values[0] is FunctionDefinition; }
        }

        public override IList<string> ProvideVariables()
        {
            var list = new List<string>();
            foreach (var target in Targets)
            {
                if (target is Variable variable)
                    list.Add(variable.Name);
            }

            return list;
        }

        public class AssignmentTargetNode : IntermediateNode
        {
            public AssignmentTargetNode(ICheckNode node) : base(node, "AssignmentTarget")
            {
            }

            public override void LookupVariable(ICheckContext context, Node child, string variable)
            {
                // base.LookupVariable(child, variable);
            }
        }

        public class AssignmentValueNode : IntermediateNode
        {
            public AssignmentValueNode(ICheckNode node) : base(node, "AssignmentValue")
            {
            }

            public override void LookupVariable(ICheckContext context, Node child, string variable)
            {
                base.LookupVariable(context, child, variable);
            }
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var target in Targets)
            {
                new AssignmentTargetNode(target).CheckNode(context, this);
            }

            foreach (var value in Values)
            {
                new AssignmentValueNode(value).CheckNode(context, this);
            }
        }

        public void ExportableVariables(ICheckContext context)
        {
            if (IsLocal == false)
            {
                for (var i = 0; i < Targets.Count; i++)
                {
                    var target = Targets[i];
                    if (target is Variable variable)
                    {
                        if (Values[i] is FunctionDefinition functionDefinition)
                        {
                            context.AddExportVariable(new ExportVariable()
                            {
                                VariableName = variable.Name,
                                IsMethod = true,
                                IsClassMethod = PloopClass != null,
                            });
                        }
                        else
                        {
                            context.AddExportVariable(new ExportVariable()
                            {
                                VariableName = variable.Name,
                                IsField = true,
                                IsClassField = PloopClass != null,
                            });
                        }
                    }
                }
            }
        }

        public void WriteNamedFunctionStyle(IndentAwareTextWriter writer, string name, FunctionDefinition func)
        {
            writer.Write("function ");
            if (PloopClass != null && !IsLocal)
            {
                var isClassMethod = Attribute?.IsStatic ?? false;
                //CUSTOM
                if (PloopClass.parent is PloopModule ploopModule)
                {
                    if (ploopModule.ModuleName.StartsWith("Game.Net."))
                    {
                        if (Targets[0] is Variable variable)
                        {
                            if (variable.Name != "__ctor")
                            {
                                isClassMethod = true;
                            }
                        }
                    }
                }
                //CUSTOM
                if (Values[0] is FunctionDefinition functionDefinition)
                {
                    var definitionArgumentNames = functionDefinition.ArgumentNames;
                    if (definitionArgumentNames.Count == 0 || definitionArgumentNames[0] != "self")
                    {
                        if (Targets[0] is Variable variable)
                        {
                            if (!variable.Name.StartsWith("__Get") && !variable.Name.StartsWith("__Set"))
                            {
                                isClassMethod = true;
                            }
                        }

                        
                    }
                }

                //CUSTOM 
                if (PloopClass?.ClassName == "MapUtil")
                {
                    isClassMethod = true;
                }

                if (isClassMethod)
                    writer.Write($"{PloopClass.ClassName}.{name}");
                else
                    writer.Write($"{PloopClass.ClassName}:{name}");
            }
            else
            {
                writer.Write(name);
            }

            // Debug.Assert(func.ArgumentNames.FirstOrDefault() != null);
            var firstArgSelf = func.ArgumentNames.FirstOrDefault() != null &&
                               func.ArgumentNames.FirstOrDefault() == "self";

            func.Write(writer, new FunctionDefinition.FunctionDefinitionData()
            {
                PloopClass = PloopClass,
                from_named = true,
                ImplicitSelf = PloopClass != null && firstArgSelf,
            });
        }

        public void WriteGenericStyle(IndentAwareTextWriter writer)
        {
            // note: local declaration is never named function style (because
            // that automatically implies there's a value assigned)

            for (var i = 0; i < Targets.Count; i++)
            {
                var target = Targets[i] as Node;
                target.Write(writer);
                if (i != Targets.Count - 1) writer.Write(", ");
            }

            if (IsLocalDeclaration) return;

            writer.Write(" = ");
            for (var i = 0; i < Values.Count; i++)
            {
                var value = Values[i] as Node;
                value.Write(writer);
                if (i != Values.Count - 1) writer.Write(", ");
            }

            if (ForceExplicitLocalNil && Values.Count < Targets.Count)
            {
                // match with nils if ForceExplicitLocalNil is set
                if (Values.Count > 0) writer.Write(", ");
                var fill_in_count = (Targets.Count - Values.Count);
                for (var i = 0; i < fill_in_count; i++)
                {
                    writer.Write("nil");
                    if (i < fill_in_count - 1) writer.Write(", ");
                }
            }
        }

        // Please see explanation in the class summary.
        public bool IsLocalDeclaration
        {
            get
            {
                if (ForceExplicitLocalNil) return false;
                if (IsLocal && Values.Count == 0) return true;
                if (IsLocal && (Targets.Count == Values.Count))
                {
                    for (var i = 0; i < Values.Count; i++)
                    {
                        if (!(Values[i] is NilLiteral)) return false;
                    }

                    return true;
                }

                return false;
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            if (data != null && data is PloopClass _ploopClass)
            {
                this.PloopClass = _ploopClass;
            }


            if (IsLocal) writer.Write("local ");

            if (Targets.Count == 1 && Values.Count == 1 && Values[0] is FunctionDefinition)
            {
                string funcname = null;

                if (Targets[0] is Variable && ((Variable)Targets[0]).Name.IsIdentifier())
                {
                    funcname = ((Variable)Targets[0]).Name;
                    WriteNamedFunctionStyle(writer, funcname, Values[0] as FunctionDefinition);
                }
                else if (Targets[0] is TableAccess)
                {
                    var func = Values[0] as FunctionDefinition;
                    funcname = ((TableAccess)Targets[0]).GetIdentifierAccessChain(is_method_access: func.ImplicitSelf);
                    if (funcname != null) WriteNamedFunctionStyle(writer, funcname, func);
                    else WriteGenericStyle(writer);
                }
                else WriteGenericStyle(writer);
            }
            else
            {
                WriteGenericStyle(writer);
            }
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Base class for for statements.
    /// </summary>
    public abstract class For : Node, IStatement
    {
        public Block Block;
    }

    /// <summary>
    /// Numeric for statement. Please note that `Step` is optional in code and
    /// defaults to 1. If the `AutofillNumericForStep` option is enabled in
    /// `Parser.Settings` (which it is by default), then the `Step` field will
    /// never be null. Instead, if the code does not specify it, it will be
    /// created automatically as a `NumberLiteral` of value 1. Without this
    /// option, the field may be null if no step was specified.
    /// 
    /// ```
    /// for i=1,10 do
    ///     print(i)
    /// end
    /// ```
    /// </summary>
    public class NumericFor : For
    {
        public string VariableName;
        public IExpression StartPoint;
        public IExpression EndPoint;
        public IExpression Step;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            StartPoint.CheckNode(context, this);
            Step.CheckNode(context, this);
            EndPoint.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("for ");
            writer.Write(VariableName);
            writer.Write(" = ");
            StartPoint.Write(writer);
            writer.Write(", ");
            EndPoint.Write(writer);
            if (Step != null && !(Step is NumberLiteral && ((NumberLiteral)Step).Value == 1))
            {
                writer.Write(", ");
                Step.Write(writer);
            }

            writer.Write(" do");
            writer.IncreaseIndent();
            writer.WriteLine();
            Block.Write(writer, false);
            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("end");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Generic for statement.
    /// 
    /// ```
    /// for k, v in pairs(some_table) do
    ///     print(tostring(k) .. " = " .. tostring(v))
    /// end
    /// 
    /// for i, v in ipairs(some_table) do
    ///     print(v)
    /// end
    /// </summary>
    public class GenericFor : For
    {
        public List<string> VariableNames = new List<string>();
        public IExpression Iterator;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            Iterator.CheckNode(context, this);
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write("for ");
            for (var i = 0; i < VariableNames.Count; i++)
            {
                writer.Write(VariableNames[i]);
                if (i < VariableNames.Count - 1) writer.Write(", ");
            }

            writer.Write(" in ");
            Iterator.Write(writer);
            writer.Write(" do");
            writer.IncreaseIndent();
            writer.WriteLine();
            Block.Write(writer, false);
            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("end");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    #region Ploop [https: //github.com/kurapica/PLoop/blob/master/README-zh.md]

    /// <summary>
    /// Module "Game.View"(function(_ENV)
    /// namespace "Game.Net"
    /// import "Game.Module"
    /// </summary>
    public class Ploop : Node, IStatement
    {
        public List<IStatement> Statements;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var statement in Statements)
            {
                statement.CheckNode(context, this);
            }
        }

        public override void LookupVariable(ICheckContext context, Node child, string variable)
        {
            var indexOf = Statements.IndexOf((IStatement)child);
            if (indexOf > 0)
            {
                for (var i = indexOf - 1; i >= 0; i--)
                {
                    var provides = Statements[i].ProvideVariables();
                    if (provides.Contains(variable))
                    {
                        // Console.WriteLine($"find variable {variable}");
                        return;
                    }
                }
            }

            base.LookupVariable(context, child, variable);
        }

        public void ExportableVariables(ICheckContext context)
        {
            foreach (var statement in Statements)
            {
                statement.ExportableVariables(context);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.WriteLine();
            var first = true;
            foreach (var statement in Statements)
            {
                if (first == false)
                    writer.WriteLine();
                statement.Write(writer);
                first = false;
            }

            writer.WriteLine();
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Module "Game.View"(function(_ENV)
    /// namespace "Game.Net"
    /// import "Game.Module"
    /// </summary>
    public class PloopModule : Node, IStatement
    {
        public string ModuleName;
        public FunctionCall NamespaceFunction;
        public List<FunctionCall> ImportFunctions = new List<FunctionCall>();
        public List<IStatement> Statements;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var statement in Statements)
            {
                statement.CheckNode(context, this);
            }
        }

        public override void LookupVariable(ICheckContext context, Node child, string variable)
        {
            var indexOf = Statements.IndexOf((IStatement)child);
            if (indexOf > 0)
            {
                for (var i = indexOf - 1; i >= 0; i--)
                {
                    var provides = Statements[i].ProvideVariables();
                    if (provides.Contains(variable))
                    {
                        // Console.WriteLine($"find variable {variable}");
                        return;
                    }
                }
            }

            base.LookupVariable(context, child, variable);
        }

        public void ExportableVariables(ICheckContext context)
        {
            foreach (var statement in Statements)
            {
                statement.ExportableVariables(context);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.WriteLine();

            if (ModuleName == "Game.Data.WorldMapData")
            {
                writer.WriteLine("""
                                 local MapCityData = require("GameData/Map/city/MapCityData")
                                 local MapNpcData = require("GameData/Map/world/MapNpcData")
                                 local MapResData = require("GameData/Map/world/MapResData")
                                 local MapTroopData = require("GameData/Map/world/MapTroopData")
                                 local MapABuildingData = require("GameData/Map/world/MapABuildingData")
                                 local CityTroopData = require("GameData/Map/obsolete/CityTroopData")
                                 local MapBookmarkData = require("GameData/Map/world/MapBookmarkData")
                                 local MapNBuildingData = require("GameData/Map/world/MapNBuildingData")
                                 """);
            }

            var first = true;
            foreach (var statement in Statements)
            {
                if (first == false)
                    writer.WriteLine();
                statement.Write(writer);
                first = false;
            }

            writer.WriteLine();
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    ///  class "SuperTreeContainer"(function(_ENV)
    ///      inherit "LuaObject"
    /// </summary>
    public class PloopClass : Node, IStatement
    {
        public string ClassName;
        public string InheritClassName;

        /// <summary>
        /// inherit class
        /// </summary>
        public PloopClass InheritClass;

        public string InheritRequirePath = string.Empty;
        public List<IStatement> Statements;
        public string RequirePath = string.Empty;

        /// <summary>
        /// Multy class in single file 
        /// </summary>
        public bool singleFileMultiClass;

        /// <summary>
        /// file name 
        /// </summary>
        public string FileName;

        //---namespace
        // public string Namespace;
        //----partial class ---
        public bool IsPartialClass = false;
        public bool IsMainPartialClass = false;
        public string MainPartialRequirePath = string.Empty;
        public List<string> SubPartialRequirePaths = new List<string>();
        public PloopClass MainPartialClass;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
            foreach (var statement in Statements)
            {
                statement.CheckNode(context, this);
            }
        }

        public override void LookupVariable(ICheckContext context, Node child, string variable)
        {
            var indexOf = Statements.IndexOf((IStatement)child);
            if (indexOf > 0)
            {
                for (var i = indexOf - 1; i >= 0; i--)
                {
                    var provides = Statements[i].ProvideVariables();
                    if (provides.Contains(variable))
                    {
                        // Console.WriteLine($"find variable {variable}");
                        return;
                    }
                }
            }

            base.LookupVariable(context, child, variable);
        }

        public void ExportableVariables(ICheckContext context)
        {
            context.AddExportVariable(new ExportVariable()
            {
                VariableName = ClassName,
                IsClass = true
            });

            foreach (var statement in Statements)
            {
                statement.ExportableVariables(context);
            }
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            //custom
            if (ClassName == "WorldMapModule" && IsMainPartialClass)
            {
                writer.WriteLine(
                    $"local MapViewPortChangedHandler = require(\"GameModule/Map/MapViewPortChangedHandler\")");
                writer.WriteLine($"--local PBEMapNode = require(\"GameData/Map/WorldMapData\")");
                writer.WriteLine(
                    $"local MapCityObjectComponent = require(\"GameModule/Map/MapComponent/MapCityObjectComponent\")");
                writer.WriteLine(
                    $"local MapObjectComponent = require(\"GameModule/Map/MapComponent/MapObjectComponent\")");
                writer.WriteLine(
                    $"local MapArmyObjectComponent = require(\"GameModule/Map/MapComponent/MapArmyObjectComponent\")");
                writer.WriteLine(
                    $"local MapNpcObjectComponent = require(\"GameModule/Map/MapComponent/MapNpcObjectComponent\")");

                writer.WriteLine("""
                                 local MapResView = require("GameModule/Map/MapUnit/MapResView")
                                 local MapTreeView = require("GameModule/Map/MapUnit/MapTreeView")
                                 local MapStaticObjView = require("GameModule/Map/MapUnit/MapStaticObjView")
                                 local MapBookmarkView = require("GameModule/Map/MapUnit/MapBookmarkView")
                                 local MapABuildingView = require("GameModule/Map/MapUnit/MapABuildingView")
                                 local MapBuildingResView = require("GameModule/Map/MapUnit/MapBuildingResView")
                                 local MapNBuildingView = require("GameModule/Map/MapUnit/MapNBuildingView")
                                 """);
            }
            else if (RequirePath.Contains("WorldMapModule_View"))
            {
                writer.WriteLine(
                    $"local MapLevel0DisplayDataProvider = require(\"GameModule/Map/Lod/MapLevel0DisplayDataProvider\")");
            }

            else if (RequirePath.Contains("Map/city/MapCityData_Building"))
            {
                writer.WriteLine("""
                                 local MapCityBuildingData = require("GameData/Map/city/MapCityBuildingData")
                                 """);
            }
            else if (RequirePath.Contains("Map/city/MapCityData_Army"))
            {
                writer.WriteLine("""
                                 local ArmyData = require("GameData/Map/city/MapCityArmyData").ArmyData
                                 """);
            }
            else if (RequirePath.Contains("GameModule/SelfCity/SelfCityModule_Build"))
            {
                writer.WriteLine("""
                                 local MapCityBuildingData = require("GameData/Map/city/MapCityBuildingData")
                                 """);
            }
            else if (ClassName == "MapNpcObjectComponent")
            {
                writer.WriteLine("""
                                 local MapNpcView = require("GameModule/Map/MapUnit/MapNpcView")
                                 """);
            }

            else if (ClassName == "MapNpcView")
            {
                writer.WriteLine("""
                                 local MapLegion = require("GameModule/Battle/MapLegion")
                                 """);
            }


            if (ClassName == "DServerData")
            {
                writer.WriteLine($"local DServerDataElement = require(\"GameData/DServerData/DServerDataElement\")");
            }
            else if (ClassName == "serverlist_panel")
            {
                writer.WriteLine($"local ViewBaseN = require(\"Common/GamePlay/GameView/ViewBase/ViewBaseN\")");
                writer.WriteLine($"local CommonContainer = require(\"Common/UI/CommonContainerN\")");
            }
            else if (ClassName == "MapLegion" && IsMainPartialClass)
            {
                writer.WriteLine("""
                                 local MapEntity = require("GameModule/Battle/MapEntity")
                                 """);
            }
            else if (RequirePath.Contains("BattleModule_Conf"))
            {
                writer.WriteLine("""
                                 local MapFormationInfo = require("GameModule/Battle/MapFormationInfo").MapFormationInfo
                                 """);
            }

            else if (RequirePath.Contains("MapLegion_Battle"))
            {
                writer.WriteLine("""
                                 local MapEntity = require("GameModule/Battle/MapEntity")
                                 """);
            }
            else if (ClassName == "MapCityObjectComponent")
            {
                writer.WriteLine("""
                                 local MapCityView = require("GameModule/Map/MapUnit/MapCityView")
                                 """);
            }
            else if (RequirePath.Contains("HomeSceneModule_Operator"))
            {
                writer.WriteLine("""
                                 local CitySelector = require("GameModule/Map/CityUnit/CitySelector")
                                 """);
                
            }
            else if (RequirePath.Contains("HomeSceneModule_Unlock"))
            {
                writer.WriteLine("""
                                 local CityNodeFactory = require("GameModule/Map/CityUnit/CityNodeFactory")
                                 """);
                
            }
            else if (ClassName == "HomeSceneModule" && IsMainPartialClass)
            {
                writer.WriteLine("""
                                 local CityNodeFactory = require("GameModule/Map/CityUnit/CityNodeFactory")
                                 """);
            }
            else if (ClassName == "CityNodeFactory")
            {
                writer.WriteLine("""
                                 local CityArmyView = require("GameModule/Map/CityUnit/CityArmyView")
                                 local CityBuildingView = require("GameModule/Map/CityUnit/CityBuildingView")
                                 local CityUnlockEventView = require("GameModule/Map/CityUnit/CityUnlockEventView")
                                 local CityDefenceView = require("GameModule/Map/CityUnit/CityDefenceView")
                                 local CityCornerObjView = require("GameModule/Map/CityUnit/CityCornerObjView")
                                 local CityEdgeObjView = require("GameModule/Map/CityUnit/CityEdgeObjView")
                                 local CityRepairedBuildingView = require("GameModule/Map/CityUnit/CityRepairedBuildingView")
                                 local CityGridBuildingBaseView = require("GameModule/Map/CityUnit/CityGridBuildingBaseView")
                                 """);
                
            }
            else if (ClassName == "CityBuildingView" || ClassName == "CityCornerObjView"|| ClassName == "CityDefenceView" ||
                     ClassName == "CityEdgeObjView" )
            {
                writer.WriteLine("""
                                 local CityNodeStates = require("GameModule/Map/State/CityNodeState").CityNodeStates
                                 """);
            }
            else if (ClassName == "CityUnlockEventView" || ClassName == "CityRepairedBuildingView")
            {
                writer.WriteLine("""
                                 local UnlockEventNodeState = require("GameModule/Map/State/UnlockEventNodeState").UnlockEventNodeState
                                 """);
            }




            writer.WriteLine($"--local {ClassName} = require(\"{RequirePath}\")");
            if (IsMainPartialClass || !IsPartialClass)
            {
                // writer.WriteLine("local class = require(\"middleclass\")");
                // ---@class Car : Transport @define class Car extends Transport
                if (string.IsNullOrEmpty(InheritClassName))
                {
                    writer.WriteLine($"---@class {ClassName}");
                    writer.WriteLine($"local {ClassName} = middleclass('{ClassName}') ");
                }
                else
                {
                    // Debug.Assert(InheritRequirePath != string.Empty,"InheritRequirePath != string.Empty");
                    if (InheritRequirePath != RequirePath)
                    {
                        if (InheritClass?.singleFileMultiClass ?? false)
                            writer.WriteLine(
                                $"local {InheritClassName} = require(\"{InheritRequirePath}\").{InheritClassName}");
                        else
                            writer.WriteLine($"local {InheritClassName} = require(\"{InheritRequirePath}\")");
                    }
                    else
                    {
                        // Console.WriteLine($"InheritRequirePath = RequirePath,{InheritRequirePath}");
                    }

                    writer.WriteLine($"---@class {ClassName} : {InheritClassName}");
                    writer.WriteLine($"local {ClassName} = middleclass('{ClassName}', {InheritClassName}) ");
                }
            }
            else
            {
                //sub partial class
                writer.WriteLine($"---------reserve in advance--------------------");
                writer.WriteLine($"package.loaded[\"{RequirePath}\"] = {{}}");
                writer.WriteLine($"---@type {ClassName}");
                writer.WriteLine($"local {ClassName} = require(\"{MainPartialRequirePath}\")");
                writer.WriteLine($"---------assign to package.load--------------------");
                writer.WriteLine($"package.loaded[\"{RequirePath}\"] = {ClassName}");
            }

            if (IsMainPartialClass || !IsPartialClass)
            {
                var property = Statements.Count(((statement => statement is PloopProperty)));
                if (true || property > 0)
                {
                    writer.WriteLine();
                    // writer.IncreaseIndent();

                    //add the __index/__newindex
                    writer.WriteLine($@"
function {ClassName}:__index(key)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2) 
    local existField = true --rawget(self,fieldName) ~= nil
    local getFunc = rawget(self.class.__instanceDict,'__Get'..key)
    if existField and getFunc ~= nil then
        return getFunc(self)
    end
    return nil
end
            ");
                    writer.WriteLine($@"
function {ClassName}:__newindex(key,value)
    local fieldName = '__' .. key:sub(1, 1):lower() .. key:sub(2)
    local existField = true-- rawget(self,fieldName) ~= nil
    local setFunc = rawget(self.class.__instanceDict,'__Set'..key)
    if existField and setFunc ~= nil then
        setFunc(self,value)
    else
        rawset(self,key,value)
    end
end
            ");
                }
            }

            // var first = true;
            foreach (var statement in Statements)
            {
//                if(first == false)
                if (statement is Assignment assignment && assignment.IsFunctionAssignment)
                    writer.WriteLine();
                statement.Write(writer, this);
                // first = false;
                writer.WriteLine();
            }

            // writer.DecreaseIndent();
            writer.WriteLine();

            if (IsMainPartialClass)
            {
                writer.WriteLine("---------auto include sub partial class--------------------");
                writer.WriteLine($"package.loaded[\"{RequirePath}\"] = {ClassName}");
                foreach (var subPartialRequirePath in SubPartialRequirePaths)
                {
                    writer.WriteLine($"if(not package.loaded[\"{subPartialRequirePath}\"]) then");
                    writer.WriteLine($"\trequire(\"{subPartialRequirePath}\")  ");
                    writer.WriteLine("end");
                }

                writer.WriteLine("---------auto include sub partial class--------------------");
                writer.WriteLine();
            }

            //CUSTOM
            if (RequirePath.Contains("WorldMapModule_View"))
            {
                writer.WriteLine("_G.WorldMapModule = WorldMapModule");
            }

            // if (singleFileMultiClass == false)
            //     writer.Write($"return {ClassName}");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            writer.Write($"class {ClassName}");
            writer.WriteLine(" {");
            writer.IncreaseIndent();
            writer.WriteLine();
            var first = true;
            foreach (var statement in Statements)
            {
//                if(first == false)
                writer.WriteLine();
                statement.Write(writer, this);
                first = false;
                writer.WriteLine();
            }

            writer.DecreaseIndent();
            writer.WriteLine();
            writer.Write("}");
        }
    }

    /// <summary>
    /// class property
    /// property "Name" { field = "__name", type = System.String, default = "unknown", set = false}
    ///
    /// ------get----- 
    /// get = "GetName" / get = function(self) return self.__name end
    /// -----set
    /// set = false / set = function(self, idx, value) self[idx] = value end, / set = "SetName",
    /// 
    /// </summary>
    public class PloopProperty : Node, IStatement
    {
        public PloopClass PloopClass;
        public string PropertyName;
        public TableConstructor PropertyTable;
        public PloopAttribute Attribute;
        private StringLiteral fieldStringLiteral;

        // default 
        private IExpression defaultLiteral;
        private FunctionDefinition defaultFunction;
        private FunctionCall defaultFunctionCall;
        private string typeString;
        private FunctionDefinition handlerFunction;

        /// <summary>
        /// property expressions 
        /// </summary>
        private Dictionary<string, IExpression> propertyExpressions;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            AnalysisExpressions();
            this.parent = parent;
            PropertyTable.CheckNode(context, this);
        }

        public override IList<string> ProvideVariables()
        {
            AnalysisExpressions();
            return new List<string>()
            {
                PropertyName, fieldStringLiteral.Value
            };
        }

        public void ExportableVariables(ICheckContext context)
        {
            context.AddExportVariable(new ExportVariable()
            {
                VariableName = PropertyName,
                IsClassField = true
            });
        }

        /// <summary>
        /// first analysis the property table 
        /// </summary>
        private void AnalysisExpressions()
        {
            if (propertyExpressions != null)
                return;
            propertyExpressions = new Dictionary<string, IExpression>();
            foreach (var entry in PropertyTable.Entries)
            {
                var key = entry.Key;
                var value = entry.Value;
                Debug.Assert(key is StringLiteral, $"key is StringLiteral,{PloopClass.ClassName} {PropertyName}");
                if (key is StringLiteral stringLiteral)
                {
                    propertyExpressions.Add(stringLiteral.Value, value);
                }
            }

            propertyExpressions.TryGetValue("field", out var _field);
            propertyExpressions.TryGetValue("type", out var _type);
            propertyExpressions.TryGetValue("default", out var _default);
            propertyExpressions.TryGetValue("get", out var _get);
            propertyExpressions.TryGetValue("set", out var _set);
            propertyExpressions.TryGetValue("handler", out var _handler);

            // type 
            if (_type != null)
            {
                typeString = _type.ToString();
            }

            //field 
            if (_field is StringLiteral _fieldStringLiteral)
            {
                fieldStringLiteral = _fieldStringLiteral;
            }
            else if (_field is null)
            {
                fieldStringLiteral = new StringLiteral()
                {
                    Value = $"__{char.ToLower(PropertyName[0]) + PropertyName.Substring(1)}"
                };
            }

            if (_default is FunctionDefinition _defaultFunction)
            {
                defaultFunction = _defaultFunction;
                Console.WriteLine($"------->ClassName:{PloopClass?.ClassName} PropertyName:{PropertyName}");
            }
            else if (_default is FunctionCall _defaultFunctionCall)
            {
                defaultFunctionCall = _defaultFunctionCall;
                Console.WriteLine($"------->>>{PloopClass?.ClassName} {PropertyName}");
            }
            else if (_default is StringLiteral || _default is NilLiteral || _default is BoolLiteral ||
                     _default is NumberLiteral || _default is TableConstructor)
            {
                defaultLiteral = _default;
            }

            if (_handler != null)
                Debug.Assert(_handler is FunctionDefinition, "handler is FunctionDefinition");
            if (_handler is FunctionDefinition _handlerFunction)
            {
                //todo 
                handlerFunction = _handlerFunction;
            }
        }

        public Assignment GetPropertyStaticFieldAssignment()
        {
            AnalysisExpressions();
            var isStatic = Attribute?.IsStatic ?? false;
            if (isStatic == false)
                return null;
            
            if (fieldStringLiteral != null)
            {
                //强制设置为nil
                var assignment = new Assignment()
                {
                    IsLocal = isStatic,
                    Targets = new()
                    {
                        new Variable()
                        {
                            Name = fieldStringLiteral.Value
                        }       
                    },
                    Values = new()
                    {
                        new NilLiteral()
                    },
                };
                return assignment;
            }
            return null;
        }

        /// <summary>
        /// get self.__field assignment
        /// </summary>
        public Assignment GetPropertyFieldAssignment()
        {
            AnalysisExpressions();

            var isStatic = Attribute?.IsStatic ?? false;
            if (fieldStringLiteral != null)
            {
                if (defaultFunction != null && !isStatic)
                {
                    var assignment = new Assignment()
                    {
                        Targets = new()
                        {
                            new TableAccess()
                            {
                                Table = new Variable()
                                {
                                    Name = "self"
                                },
                                Index = fieldStringLiteral,
                            }
                        },
                        Values = new()
                        {
                            new FunctionCall()
                            {
                                Function = defaultFunction,
                                Arguments = new List<IExpression>()
                                {
                                    new Variable()
                                    {
                                        Name = "self"
                                    }
                                }
                            }
                        },

                    };
                    return assignment;
                }

                if (defaultFunctionCall != null && !isStatic)
                {
                    //todo only luaobject
                    // Debug.Assert(PloopClass.ClassName == "LuaObject");

                    var assignment = new Assignment()
                    {
                        Targets = new()
                        {
                            new TableAccess()
                            {
                                Table = new Variable()
                                {
                                    Name = "self"
                                },
                                Index = fieldStringLiteral,
                            }
                        },
                        Values = new()
                        {
                            defaultFunctionCall
                        },
                    };
                    return assignment;
                }
                else if (defaultLiteral != null)
                {
                    var assignment = new Assignment()
                    {
                        // IsLocal = isStatic,
                        Targets = new()
                        {
                            isStatic
                                ? new Variable()
                                {
                                    Name = fieldStringLiteral.Value
                                }
                                : new TableAccess()
                                {
                                    Table = new Variable()
                                    {
                                        Name = "self"
                                    },
                                    Index = fieldStringLiteral,
                                }
                        },
                        Values = new()
                        {
                            defaultLiteral
                        },
                    };
                    return assignment;
                }
            }
            //     else
            //     {
            //         // return null;
            //         IExpression defaultValueExpression = new NilLiteral();
            //         if (string.IsNullOrEmpty(typeString) == false)
            //         {
            //             if (typeString == "System.Integer" ||
            //                 typeString == "Integer" || typeString == "System.Number" || typeString == "System.number" ||
            //                 typeString == "Number")
            //             {
            //                 defaultValueExpression = new NumberLiteral()
            //                 {
            //                     Value = 0,
            //                 };
            //             }
            //             else if (typeString == "System.String" ||
            //                      typeString == "String")
            //             {
            //                 defaultValueExpression = new StringLiteral()
            //                 {
            //                     Value = "",
            //                 };
            //             }
            //             else if (typeString == "System.Table" || typeString == "Table")
            //             {
            //                 defaultValueExpression = new TableConstructor()
            //                 {
            //                 };
            //             }
            //             else if (typeString == "System.Boolean" || typeString == "Boolean")
            //             {
            //                 defaultValueExpression = new BoolLiteral()
            //                 {
            //                     Value = false,
            //                 };
            //             }
            //             else if (typeString == "System.Class")
            //             {
            //                 defaultValueExpression = new NilLiteral();
            //             }
            //             else
            //             {
            //                 throw new NotImplementedException();
            //             }
            //         }
            //         //强制设置为nil
            //         defaultValueExpression = new NilLiteral();
            //         var assignment = new Assignment()
            //         {
            //             IsLocal = isStatic,
            //             Targets = new()
            //             {
            //                 isStatic
            //                     ? new Variable()
            //                     {
            //                         Name = fieldStringLiteral.Value
            //                     }
            //                     : new TableAccess()
            //                     {
            //                         Table = new Variable()
            //                         {
            //                             Name = "self"
            //                         },
            //                         Index = fieldStringLiteral,
            //                     }
            //             },
            //             Values = new()
            //             {
            //                 defaultValueExpression
            //             },
            //         };
            //         return assignment;
            //     }
            // }

            return null;
        }

        /// <summary>
        /// get property assignment 
        /// </summary>
        /// <returns></returns>
        public List<Assignment> GetPropertyFunction()
        {
            AnalysisExpressions();
            var isStatic = Attribute?.IsStatic ?? false;
            var assignments = new List<Assignment>();
            if (handlerFunction != null)
            {
                assignments.Add(new Assignment()
                {
                    PloopClass = PloopClass,
                    Targets = new List<IAssignable>()
                    {
                        new Variable()
                        {
                            Name = $"__On{PropertyName}Handler",
                        },
                    },
                    Values = new List<IExpression>()
                    {
                        handlerFunction
                    },
                });
            }

            //get is a function 
            propertyExpressions.TryGetValue("get", out var _get);
            if (_get is FunctionDefinition _getFunction)
            {
                assignments.Add(new Assignment()
                {
                    PloopClass = PloopClass,
                    Attribute = Attribute,
                    Targets = new List<IAssignable>()
                    {
                        new Variable()
                        {
                            Name = $"__Get{PropertyName}",
                        },
                    },
                    Values = new List<IExpression>()
                    {
                        _getFunction
                    },
                });
            }
            else if ((_get is BoolLiteral _getBoolLiteral && _getBoolLiteral.Value != false) || _get is null)
            {
                var defaultValue = GetPropertyFieldAssignment();
                If ifassignment = null;
                if (defaultValue != null)
                {
                    ifassignment = new If()
                    {
                        MainIf = new ConditionalBlock()
                        {
                            Condition = new BinaryOp()
                            {
                                
                                Left = isStatic == false?new TableAccess()
                                {
                                    Table = new Variable()
                                    {
                                        Name = "self"
                                    },
                                    Index = fieldStringLiteral
                                }: new Variable(){Name = fieldStringLiteral.Value},
                                Right = new NilLiteral(),
                                Type = BinaryOp.OpType.Equal
                            },
                            Block = new Block()
                            {
                                Statements = new List<IStatement>()
                                {
                                    defaultValue
                                }
                            }
                        },
                    };
                }

                assignments.Add(new Assignment()
                {
                    PloopClass = PloopClass,
                    Attribute = Attribute,
                    Targets = new List<IAssignable>()
                    {
                        new Variable()
                        {
                            Name = $"__Get{PropertyName}",
                        },
                    },
                    Values = new List<IExpression>()
                    {
                        new FunctionDefinition()
                        {
                            Block = new Block()
                            {
                                Statements = new List<IStatement>()
                                {
                                    //这里添加默认值 
                                    ifassignment,
                                    
                                    new Return()
                                    {
                                        Expressions = new List<IExpression>()
                                        {
                                            Attribute?.IsStatic ?? false
                                                ? new Variable()
                                                {
                                                    Name = fieldStringLiteral.Value,
                                                }
                                                : new TableAccess()
                                                {
                                                    Table = new Variable()
                                                    {
                                                        Name = "self"
                                                    },
                                                    Index = fieldStringLiteral,
                                                }
                                        }
                                    }
                                }
                            }
                        }
                    },
                });
            }
            else if (_get is BoolLiteral _getBoolLiteral1 && _getBoolLiteral1.Value == false)
            {
            }
            else
            {
                Debug.Assert(false, "_get property");
            }

            propertyExpressions.TryGetValue("set", out var _set);
            if (_set is FunctionDefinition _setFunction)
            {
                Debug.Assert(fieldStringLiteral != null, $"fieldStringLiteral is not null");
                assignments.Add(new Assignment()
                {
                    PloopClass = PloopClass,
                    Attribute = Attribute,
                    Targets = new List<IAssignable>()
                    {
                        new Variable()
                        {
                            Name = $"__Set{PropertyName}",
                        },
                    },
                    Values = new List<IExpression>()
                    {
                        _setFunction
                    },
                });
            }
            else if ((_set is BoolLiteral _boolLiteral && _boolLiteral.Value != false) || _set is null)
            {
                Debug.Assert(fieldStringLiteral != null, $"fieldStringLiteral is not null");
                assignments.Add(new Assignment()
                {
                    Attribute = Attribute,
                    PloopClass = PloopClass,
                    Targets = new List<IAssignable>()
                    {
                        new Variable()
                        {
                            Name = $"__Set{PropertyName}",
                        }
                    },
                    Values = new List<IExpression>()
                    {
                        new FunctionDefinition()
                        {
                            ArgumentNames = new List<string>()
                            {
                                "value"
                            },
                            Block = new Block()
                            {
                                Statements = new List<IStatement>()
                                {
                                    new Assignment()
                                    {
                                        Targets = new()
                                        {
                                            Attribute?.IsStatic ?? false
                                                ? new Variable()
                                                {
                                                    Name = fieldStringLiteral.Value,
                                                }
                                                : new TableAccess()
                                                {
                                                    Table = new Variable()
                                                    {
                                                        Name = "self"
                                                    },
                                                    Index = fieldStringLiteral,
                                                }
                                        },
                                        Values = new()
                                        {
                                            new Variable() { Name = "value" }
                                        },
                                    }
                                }
                            }
                        }
                    }
                });
            }
            else if (_set is BoolLiteral _boolLiteral1 && _boolLiteral1.Value == false)
            {
            }
            else
            {
                Debug.Assert(false, "_set property");
            }

            return assignments;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            writer.Write($"--PLoop Property:{PropertyName}");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }


    /// <summary>
    /// parse ploop enum type
    /// enum "name" { -- key-value pairs }
    /// enum "Direction" { North = 1, East = 2, South = 3, West = 4 }
    /// </summary>
    public class PloopEnum : Node, IStatement
    {
        public string EnumName;
        public TableConstructor enumStruct;

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override IList<string> ProvideVariables()
        {
            return [EnumName];
        }

        public void ExportableVariables(ICheckContext context)
        {
            context.AddExportVariable(new ExportVariable()
            {
                IsField = true,
                VariableName = EnumName
            });
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
            foreach (var entry in enumStruct.Entries)
            {
                var key = entry.Key;
                var value = entry.Value;
                if (key is StringLiteral stringLiteral)
                {
                }
                else if (key is NumberLiteral numberLiteral)
                {
                    entry.Key = entry.Value;
                }
            }

            writer.Write($"{EnumName} = ");
            enumStruct.Write(writer, null);
            writer.WriteLine();
            //assign meta table and call __call function 

            writer.WriteLine($@"
setmetatable({EnumName}, {{
    __call = function(self, value)
        for key, val in pairs(self) do
            if val == value then
                return key
            end
        end
        return """"
    end
}})
            ");
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// __Static__()
    /// __Indexer__(String)
    /// __Abstract__()
    /// __Sealed__()
    /// __AutoIndex__()
    /// </summary>
    public class PloopAttribute : Node, IStatement
    {
        public FunctionCall FunctionCall;

        public bool IsStatic
        {
            get
            {
                if (FunctionCall.Function is Variable variable)
                {
                    return variable.Name == "__Static__";
                }

                return false;
            }
        }

        public override void CheckNode(ICheckContext context, Node parent)
        {
            this.parent = parent;
        }

        public override void Write(IndentAwareTextWriter writer, object data)
        {
        }

        public override void Write2TS(IndentAwareTextWriter writer, object data = null)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}