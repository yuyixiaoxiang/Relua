using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Lua.AST;

namespace Lua
{
    public class Parser
    {
        /// <summary>
        /// Settings which control certain behavior of the parser.
        /// </summary>
        public class Settings
        {
            /// <summary>
            /// Automatically creates NumberLiterals for sequential elements in
            /// a table constructor (ones that do not have a key specified).
            /// 
            /// Note that if this option is `false`, `AST.TableConstructor.Entry`'s
            /// `Key` field may be `null`. That field will never be `null` if this
            /// option is set to `true`.
            /// </summary>
            public bool AutofillSequentialKeysInTableConstructor = true;

            /// <summary>
            /// Automatically creates NilLiterals for all values of empty local
            /// assignments (in the style of `local a`).
            /// 
            /// Note that if this option is `false`, `AST.Assignment`'s `Values`
            /// list will be empty for local declarations. If it is set to the
            /// default `true`, the `Values` list will always match the `Targets`
            /// list in size in that case with all entries being `NilLiteral`s.
            /// </summary>
            public bool AutofillValuesInLocalDeclaration = true;

            /// <summary>
            /// Automatically fills in the `Step` field of `AST.NumericFor` with
            /// a `NumberLiteral` of value `1` if the statement did not specify
            /// the step expression.
            /// </summary>
            public bool AutofillNumericForStep = true;

            /// <summary>
            /// If `true`, will parse LuaJIT long numbers (in the form `0LL`)
            /// into the special AST node `AST.LuaJITLongLiteral`.
            /// </summary>
            public bool EnableLuaJITLongs = true;

            /// <summary>
            /// There are certain syntax quirks such as accessing the fields of
            /// a string literal (e.g. "abc":match(...)) which Lua will throw a
            /// syntax error upon seeing, but the Relua parser will happily accept
            /// (and correctly write). If this option is enabled, all Lua behavior
            /// is imitated, including errors where they are not strictly necessary.
            /// </summary>
            public bool MaintainSyntaxErrorCompatibility = false;
        }

        public Tokenizer Tokenizer;
        public Settings ParserSettings;

        public Token CurToken;
        public Token LastToken;

        public void Move()
        {
            if (CurToken.Type == TokenType.EOF) return;
            LastToken = CurToken;
            CurToken = Tokenizer.NextToken();
        }

        public Token PeekToken => Tokenizer.PeekToken;

        public void Throw(string msg, Token tok)
        {
            throw new ParserException(msg, tok.Region);
        }

        public void ThrowExpect(string expected, Token tok)
        {
            throw new ParserException($"Expected {expected}, got {tok.Type} ({tok.Value.Inspect()})", tok.Region);
        }

        public Parser(string data, Settings settings = null) : this(new Tokenizer(data, settings), settings)
        {
        }

        public Parser(StreamReader r, Settings settings = null) : this(new Tokenizer(r.ReadToEnd(), settings), settings)
        {
        }

        public Parser(Tokenizer tokenizer, Settings settings = null)
        {
            ParserSettings = settings ?? new Settings();
            Tokenizer = tokenizer;
            CurToken = tokenizer.NextToken();
        }

        public NilLiteral ReadNilLiteral()
        {
            if (CurToken.Value != "nil") ThrowExpect("nil", CurToken);
            Move();
            return NilLiteral.Instance;
        }

        public VarargsLiteral ReadVarargsLiteral()
        {
            if (CurToken.Value != "...") ThrowExpect("varargs literal", CurToken);
            Move();
            return VarargsLiteral.Instance;
        }

        public BoolLiteral ReadBoolLiteral()
        {
            var value = false;
            if (CurToken.Value == "true") value = true;
            else if (CurToken.Value == "false") value = false;
            else ThrowExpect("bool literal", CurToken);
            Move();
            return value ? BoolLiteral.TrueInstance : BoolLiteral.FalseInstance;
        }

        public Variable ReadVariable()
        {
            if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
            if (Tokenizer.RESERVED_KEYWORDS.Contains(CurToken.Value))
                Throw($"Cannot use reserved keyword '{CurToken.Value}' as variable name", CurToken);

            var name = CurToken.Value;

            Move();
            return new Variable { Name = name };
        }

        public StringLiteral ReadStringLiteral()
        {
            if (CurToken.Type != TokenType.QuotedString) ThrowExpect("quoted string", CurToken);
            var value = CurToken.Value;
            Move();
            return new StringLiteral { Value = value };
        }

        public NumberLiteral ReadNumberLiteral()
        {
            if (CurToken.Type != TokenType.Number) ThrowExpect("number", CurToken);

            if (CurToken.Value.StartsWith("0x", StringComparison.InvariantCulture))
            {
                if (!int.TryParse(CurToken.Value.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier,
                        null, out int hexvalue))
                {
                    ThrowExpect("hex number", CurToken);
                }

                Move();

                return new NumberLiteral { Value = hexvalue, HexFormat = true };
            }

            if (!double.TryParse(CurToken.Value, out double value))
            {
                ThrowExpect("number", CurToken);
            }

            Move();
            return new NumberLiteral { Value = value };
        }

        public LuaJITLongLiteral ReadLuaJITLongLiteral()
        {
            if (CurToken.Type != TokenType.Number) ThrowExpect("long number", CurToken);
            if (CurToken.Value.StartsWith("0x", StringComparison.InvariantCulture))
            {
                if (!long.TryParse(CurToken.Value,
                        System.Globalization.NumberStyles.HexNumber |
                        System.Globalization.NumberStyles.AllowHexSpecifier, null, out long hexvalue))
                {
                    ThrowExpect("hex number", CurToken);
                }

                Move();

                return new LuaJITLongLiteral { Value = hexvalue, HexFormat = true };
            }

            if (!long.TryParse(CurToken.Value, out long value))
            {
                ThrowExpect("number", CurToken);
            }

            Move();
            if (!CurToken.IsIdentifier("LL")) ThrowExpect("'LL' suffix", CurToken);
            Move();
            return new LuaJITLongLiteral { Value = value };
        }

        public TableAccess ReadTableAccess(IExpression table_expr, bool allow_colon = false)
        {
            TableAccess table_node = null;

            if (CurToken.IsPunctuation(".") || (allow_colon && CurToken.IsPunctuation(":")))
            {
                Move();
                if (CurToken.Type != TokenType.Identifier)
                {
                    var err = true;
                    //fix like ui.property
                    if (CurToken.Value == "property" || CurToken.Value == "Module")
                    {
                        CurToken.Type = TokenType.Identifier;
                        err = false;
                    }

                    if (err)
                        ThrowExpect("identifier", CurToken);
                }

                var index = new StringLiteral { Value = CurToken.Value };
                Move();
                table_node = new TableAccess { Table = table_expr, Index = index };
            }
            else if (CurToken.IsPunctuation("["))
            {
                Move();
                var index = ReadExpression();
                if (!CurToken.IsPunctuation("]")) ThrowExpect("closing bracket", CurToken);
                Move();
                table_node = new TableAccess { Table = table_expr, Index = index };
            }
            else ThrowExpect("table access", CurToken);

            return table_node;
        }

        public FunctionCall ReadFunctionCall(IExpression func_expr, IExpression self_expr = null)
        {
            if (!CurToken.IsPunctuation("(")) ThrowExpect("start of argument list", CurToken);
            Move();

            var args = new List<IExpression>();

            if (self_expr != null)
            {
                args.Add(self_expr);
            }

            if (!CurToken.IsPunctuation(")")) args.Add(ReadExpression());

            while (CurToken.IsPunctuation(","))
            {
                Move();
                var expr = ReadExpression();
                args.Add(expr);
                if (!CurToken.IsPunctuation(",") && !CurToken.IsPunctuation(")"))
                    ThrowExpect("comma or end of argument list", CurToken);
            }

            if (!CurToken.IsPunctuation(")")) ThrowExpect("end of argument list", CurToken);
            Move();

            return new FunctionCall { Function = func_expr, Arguments = args };
        }


        public TableConstructor.Entry ReadTableConstructorEntry()
        {
            if (CurToken.Type == TokenType.Identifier)
            {
                var eq = PeekToken;
                if (eq.IsPunctuation("="))
                {
                    // { a = ... }

                    var key = new StringLiteral { Value = CurToken.Value };
                    Move();
                    Move(); // =
                    var value = ReadExpression();
                    return new TableConstructor.Entry { ExplicitKey = true, Key = key, Value = value };
                }
                else
                {
                    // { a }
                    var value = ReadExpression();
                    return new TableConstructor.Entry { ExplicitKey = false, Value = value };
                    // Note - Key is null
                    // This is filled in in ReadTableConstructor
                }
            }
            else if (CurToken.IsPunctuation("["))
            {
                // { [expr] = ... }
                Move();
                var key = ReadExpression();
                if (!CurToken.IsPunctuation("]")) ThrowExpect("end of key", CurToken);
                Move();
                if (!CurToken.IsPunctuation("=")) ThrowExpect("assignment", CurToken);
                Move();
                var value = ReadExpression();
                return new TableConstructor.Entry { ExplicitKey = true, Key = key, Value = value };
            }
            else
            {
                // { expr }
                return new TableConstructor.Entry { ExplicitKey = false, Value = ReadExpression() };
                // Note - Key is null
                // This is filled in in ReadTableConstructor
            }
        }

        public TableConstructor ReadTableConstructor()
        {
            if (!CurToken.IsPunctuation("{")) ThrowExpect("table constructor", CurToken);
            Move();

            var entries = new List<TableConstructor.Entry>();

            var cur_sequential_idx = 1;

            if (!CurToken.IsPunctuation("}"))
            {
                var ent = ReadTableConstructorEntry();
                if (ParserSettings.AutofillSequentialKeysInTableConstructor && ent.Key == null)
                {
                    ent.Key = new NumberLiteral { Value = cur_sequential_idx };
                    cur_sequential_idx += 1;
                }

                entries.Add(ent);
            }

            //fix error use ; not ,
            if (CurToken.IsPunctuation(";"))
                CurToken.Value = ",";

            while (CurToken.IsPunctuation(","))
            {
                Move();
                if (CurToken.IsPunctuation("}")) break; // trailing comma
                var ent = ReadTableConstructorEntry();
                if (ParserSettings.AutofillSequentialKeysInTableConstructor && ent.Key == null)
                {
                    ent.Key = new NumberLiteral { Value = cur_sequential_idx };
                    cur_sequential_idx += 1;
                }

                entries.Add(ent);

                //fix error use ; not ,
                if (CurToken.IsPunctuation(";"))
                    CurToken.Value = ",";

                if (!CurToken.IsPunctuation(",") && !CurToken.IsPunctuation("}"))
                    ThrowExpect("comma or end of entry list", CurToken);
            }

            if (!CurToken.IsPunctuation("}")) ThrowExpect("end of entry list", CurToken);
            Move();

            return new TableConstructor { Entries = entries };
        }

        public FunctionDefinition ReadFunctionDefinition(bool start_from_params = false, bool self = false)
        {
            if (!start_from_params)
            {
                if (!CurToken.IsKeyword("function")) ThrowExpect("function", CurToken);
                Move();
            }

            if (!CurToken.IsPunctuation("(")) ThrowExpect("start of argument name list", CurToken);
            Move();

            var varargs = false;
            var args = new List<string>();

            if (self) args.Add("self");

            if (!CurToken.IsPunctuation(")"))
            {
                //fix function(...)
                if (CurToken.IsPunctuation("..."))
                {
                    varargs = true;
                    CurToken.Type = TokenType.Identifier;
                }

                if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
                args.Add(CurToken.Value);
                Move();
            }

            while (CurToken.IsPunctuation(","))
            {
                Move();
                if (CurToken.IsPunctuation("..."))
                {
                    varargs = true;
                    Move();
                    break;
                }

                //fix params has ploop class keyword
                // function AddTypeClass(self,type,class) 
                if (CurToken.IsKeyword("class"))
                {
                    CurToken.Type = TokenType.Identifier;
                }

                if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
                args.Add(CurToken.Value);
                Move();
            }

            if (!CurToken.IsPunctuation(")")) ThrowExpect("end of argument name list", CurToken);
            Move();

            SkipSemicolons();

            var statements = new List<IStatement>();
            while (!CurToken.IsKeyword("end") && !CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            Move();

            return new FunctionDefinition
            {
                ArgumentNames = args,
                Block = new Block { Statements = statements },
                AcceptsVarargs = varargs,
                ImplicitSelf = self
            };
        }

        //主表达式 
        // Primary expression:
        // - Does not depend on any expressions.
        public IExpression ReadPrimaryExpression()
        {
            if (CurToken.Type == TokenType.QuotedString)
            {
                return ReadStringLiteral();
            }

            if (CurToken.Type == TokenType.Number)
            {
                if (ParserSettings.EnableLuaJITLongs && PeekToken.IsIdentifier("LL"))
                {
                    return ReadLuaJITLongLiteral();
                }
                else
                {
                    return ReadNumberLiteral();
                }
            }

            if (CurToken.Type == TokenType.Punctuation)
            {
                if (CurToken.Value == "{") return ReadTableConstructor();
                if (CurToken.Value == "...") return ReadVarargsLiteral();
            }
            else if (CurToken.Type == TokenType.Keyword)
            {
                if (CurToken.Value == "nil") return ReadNilLiteral();
                if (CurToken.Value == "true" || CurToken.Value == "false")
                {
                    return ReadBoolLiteral();
                }

                if (CurToken.Value == "function")
                {
                    return ReadFunctionDefinition();
                }
            }
            else if (CurToken.Type == TokenType.Identifier)
            {
                return ReadVariable();
            }

            ThrowExpect("expression", CurToken);
            throw new Exception("unreachable");
        }

        public OperatorInfo? GetBinaryOperator(Token tok)
        {
            if (tok.Value == null) return null;
            var op = OperatorInfo.FromToken(tok);
            if (op == null) return null;
            if (!op.Value.IsBinary) ThrowExpect("binary operator", tok);

            return op.Value;
        }

        //读取次要表达式
        // Secondary expression:
        // - Depends on (alters the value of) *one* expression.
        // fix fun_args whether is function args
        public IExpression ReadSecondaryExpression()
        {
            //判断是一元还是二元操作符
            var unary_op = OperatorInfo.FromToken(CurToken);

            if (unary_op != null && unary_op.Value.IsUnary)
            {
                var realUnaryOp = true;
                //fix [aa("+")]
                if ( (PeekToken.IsPunctuation(")") || PeekToken.IsPunctuation(",") || PeekToken.IsPunctuation("..")||
                                 (LastToken.IsPunctuation(".."))) )  
                {
                    unary_op = null;
                    realUnaryOp = false;
                }

                if (realUnaryOp)
                    Move();
            }

            IExpression expr;
            if (CurToken.IsPunctuation("("))
            {
                Move();
                var complex = ReadComplexExpression(ReadSecondaryExpression(), 0, true);
                if (!CurToken.IsPunctuation(")"))
                {
                    ThrowExpect("closing parenthesis", CurToken);
                }

                Move();
                expr = complex;
                if (expr is FunctionCall)
                {
                    ((FunctionCall)expr).ForceTruncateReturnValues = true;
                }
            }
            else expr = ReadPrimaryExpression();


            do
            {
                while (CurToken.IsPunctuation(".") || CurToken.IsPunctuation("["))
                {
                    if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                    if (expr is StringLiteral && ParserSettings.MaintainSyntaxErrorCompatibility)
                    {
                        Throw($"syntax error compat: can't directly index strings, use parentheses", CurToken);
                    }

                    expr = ReadTableAccess(expr);
                }

                while (CurToken.IsPunctuation(":"))
                {
                    if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                    if (expr is StringLiteral && ParserSettings.MaintainSyntaxErrorCompatibility)
                    {
                        Throw($"syntax error compat: can't directly index strings, use parentheses", CurToken);
                    }

                    var self_expr = expr;
                    expr = ReadTableAccess(expr, allow_colon: true);
                    expr = ReadFunctionCall(expr, self_expr);
                }

                if (CurToken.IsPunctuation("("))
                {
                    if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                    if (expr is StringLiteral && ParserSettings.MaintainSyntaxErrorCompatibility)
                    {
                        Throw($"syntax error compat: can't directly call strings, use parentheses", CurToken);
                    }

                    expr = ReadFunctionCall(expr);
                }
                else if (CurToken.IsPunctuation("{"))
                {
                    if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                    if (expr is StringLiteral && ParserSettings.MaintainSyntaxErrorCompatibility)
                    {
                        Throw($"syntax error compat: can't directly call strings, use parentheses", CurToken);
                    }

                    expr = new FunctionCall
                    {
                        Function = expr,
                        Arguments = new List<IExpression> { ReadTableConstructor() }
                    };
                }
                else if (CurToken.Type == TokenType.QuotedString)
                {
                    if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                    if (expr is StringLiteral && ParserSettings.MaintainSyntaxErrorCompatibility)
                    {
                        Throw($"syntax error compat: can't directly call strings, use parentheses", CurToken);
                    }

                    expr = new FunctionCall
                    {
                        Function = expr,
                        Arguments = new List<IExpression> { ReadStringLiteral() }
                    };
                }
            } while (CurToken.IsPunctuation(".") || CurToken.IsPunctuation(":") || CurToken.IsPunctuation("["));


            if (unary_op != null && unary_op.Value.IsUnary)
            {
                if (expr is FunctionCall) ((FunctionCall)expr).ForceTruncateReturnValues = false;

                expr = new UnaryOp(unary_op.Value.UnaryOpType.Value, expr);
            }

            return expr;
        }

        // Complex expression:
        // - Depends on (alters the value of) *two* expressions.
        public IExpression ReadComplexExpression(IExpression lhs, int prev_op_prec, bool in_parens, int depth = 0)
        {
            var lookahead = GetBinaryOperator(CurToken);
            if (lookahead == null) return lhs;

            //Console.WriteLine($"{new string(' ', depth)}RCE: lhs = {lhs} lookahead = {lookahead.Value.TokenValue} prev_op_prec = {prev_op_prec}");

            if (lhs is FunctionCall)
            {
                ((FunctionCall)lhs).ForceTruncateReturnValues = false;
                // No need to force this (and produce extra parens),
                // because the binop truncates the return value anyway
            }

            while (lookahead.Value.Precedence >= prev_op_prec)
            {
                var op = lookahead;
                Move();
                var rhs = ReadSecondaryExpression();
                if (rhs is FunctionCall)
                {
                    ((FunctionCall)rhs).ForceTruncateReturnValues = false;
                }

                lookahead = GetBinaryOperator(CurToken);
                if (lookahead == null) return new BinaryOp(op.Value.BinaryOpType.Value, lhs, rhs);
                //Console.WriteLine($"{new string(' ', depth)}OUT rhs = {rhs} lookahead = {lookahead.Value.TokenValue} prec = {lookahead.Value.Precedence}");

                while (lookahead.Value.RightAssociative
                           ? (lookahead.Value.Precedence == op.Value.Precedence)
                           : (lookahead.Value.Precedence > op.Value.Precedence))
                {
                    rhs = ReadComplexExpression(rhs, lookahead.Value.Precedence, in_parens, depth + 1);
                    //Console.WriteLine($"{new string(' ', depth)}IN rhs = {rhs} lookahead = {lookahead.Value.TokenValue}");
                    lookahead = GetBinaryOperator(CurToken);
                    if (lookahead == null) return new BinaryOp(op.Value.BinaryOpType.Value, lhs, rhs);
                }

                lhs = new BinaryOp(op.Value.BinaryOpType.Value, lhs, rhs);
            }

            return lhs;
        }

        /// <summary>
        /// Reads a single expression.
        /// </summary>
        /// <returns>The expression.</returns>
        public IExpression ReadExpression()
        {
            var expr = ReadSecondaryExpression();
            return ReadComplexExpression(expr, 0, false);
        }

        public Break ReadBreak()
        {
            if (!CurToken.IsKeyword("break")) ThrowExpect("break statement", CurToken);
            Move();
            return new Break();
        }

        public Return ReadReturn()
        {
            if (!CurToken.IsKeyword("return")) ThrowExpect("return statement", CurToken);
            Move();

            //fix error
            while (CurToken.IsPunctuation(";"))
            {
                Move();
            }

            //fix error [return else] or [return elseif]
            if (CurToken.IsKeyword("else") || CurToken.IsKeyword("elseif"))
            {
                return new Return { Redundant = true };
            }

            var ret_vals = new List<IExpression>();

            if (!CurToken.IsKeyword("end"))
            {
                ret_vals.Add(ReadExpression());
            }

            while (CurToken.IsPunctuation(","))
            {
                Move();
                ret_vals.Add(ReadExpression());
            }

            return new Return { Expressions = ret_vals };
        }

        public If ReadIf()
        {
            if (!CurToken.IsKeyword("if")) ThrowExpect("if statement", CurToken);

            Move();

            var cond = ReadExpression();

            if (!CurToken.IsKeyword("then")) ThrowExpect("'then' keyword", CurToken);
            Move();

            var statements = new List<IStatement>();

            while (!CurToken.IsKeyword("else") && !CurToken.IsKeyword("elseif") && !CurToken.IsKeyword("end") &&
                   !CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            var mainif_cond_block = new ConditionalBlock
            {
                Block = new Block { Statements = statements },
                Condition = cond
            };

            var elseifs = new List<ConditionalBlock>();

            while (CurToken.IsKeyword("elseif"))
            {
                Move();
                var elseif_cond = ReadExpression();
                if (!CurToken.IsKeyword("then")) ThrowExpect("'then' keyword", CurToken);
                Move();
                var elseif_statements = new List<IStatement>();
                while (!CurToken.IsKeyword("else") && !CurToken.IsKeyword("elseif") && !CurToken.IsKeyword("end") &&
                       !CurToken.IsEOF())
                {
                    elseif_statements.Add(ReadStatement());
                }

                elseifs.Add(new ConditionalBlock
                {
                    Block = new Block { Statements = elseif_statements },
                    Condition = elseif_cond
                });
            }

            Block else_block = null;

            if (CurToken.IsKeyword("else"))
            {
                Move();
                var else_statements = new List<IStatement>();
                while (!CurToken.IsKeyword("end") && !CurToken.IsEOF())
                {
                    else_statements.Add(ReadStatement());
                }

                else_block = new Block { Statements = else_statements };
            }

            if (!CurToken.IsKeyword("end")) ThrowExpect("'end' keyword", CurToken);
            Move();

            return new If
            {
                MainIf = mainif_cond_block,
                ElseIfs = elseifs,
                Else = else_block
            };
        }

        public void SkipSemicolons()
        {
            while (CurToken.IsPunctuation(";")) Move();
        }

        public While ReadWhile()
        {
            if (!CurToken.IsKeyword("while")) ThrowExpect("while statement", CurToken);

            Move();
            var cond = ReadExpression();

            if (!CurToken.IsKeyword("do")) ThrowExpect("'do' keyword", CurToken);
            Move();

            SkipSemicolons();

            var statements = new List<IStatement>();

            while (!CurToken.IsKeyword("end") && !CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            Move();

            return new While
            {
                Condition = cond,
                Block = new Block { Statements = statements }
            };
        }

        public Assignment TryReadFullAssignment(bool certain_assign, IExpression start_expr, Token expr_token)
        {
            // certain_assign should be set to true if we know that
            // what we have is definitely an assignment
            // that allows us to handle implicit nil assignments (local
            // declarations without a value) as an Assignment node

            if (certain_assign || (CurToken.IsPunctuation("=") || CurToken.IsPunctuation(",")))
            {
                if (!(start_expr is IAssignable)) ThrowExpect("assignable expression", expr_token);

                var assign_exprs = new List<IAssignable> { start_expr as IAssignable };

                while (CurToken.IsPunctuation(","))
                {
                    Move();
                    start_expr = ReadExpression();
                    if (!(start_expr is IAssignable)) ThrowExpect("assignable expression", expr_token);

                    assign_exprs.Add(start_expr as IAssignable);
                }

                if (certain_assign && !CurToken.IsPunctuation("="))
                {
                    // implicit nil assignment/local declaration

                    var local_decl = new Assignment
                    {
                        IsLocal = true,
                        Targets = assign_exprs
                    };

                    if (ParserSettings.AutofillValuesInLocalDeclaration)
                    {
                        // Match Values with NilLiterals
                        for (var i = 0; i < assign_exprs.Count; i++)
                        {
                            local_decl.Values.Add(NilLiteral.Instance);
                        }
                    }

                    return local_decl;
                }

                return ReadAssignment(assign_exprs);
            }


            return null;
        }

        public Assignment ReadAssignment(List<IAssignable> assignable_exprs, bool local = false)
        {
            if (!CurToken.IsPunctuation("=")) ThrowExpect("assignment", CurToken);
            Move();
            var value_exprs = new List<IExpression> { ReadExpression() };

            while (CurToken.IsPunctuation(","))
            {
                Move();
                value_exprs.Add(ReadExpression());
            }

            return new Assignment
            {
                IsLocal = local,
                Targets = assignable_exprs,
                Values = value_exprs,
            };
        }

        public Assignment ReadNamedFunctionDefinition()
        {
            if (!CurToken.IsKeyword("function")) ThrowExpect("function", CurToken);
            Move();
            if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
            IAssignable expr = new Variable { Name = CurToken.Value };
            Move();
            while (CurToken.IsPunctuation("."))
            {
                Move();
                if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
                expr = new TableAccess
                {
                    Table = expr as IExpression,
                    Index = new StringLiteral { Value = CurToken.Value }
                };
                Move();
            }

            var is_method_def = false;
            if (CurToken.IsPunctuation(":"))
            {
                is_method_def = true;
                Move();
                if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
                expr = new TableAccess
                {
                    Table = expr as IExpression,
                    Index = new StringLiteral { Value = CurToken.Value }
                };
                Move();
            }

            var func_def = ReadFunctionDefinition(start_from_params: true, self: is_method_def);
            return new Assignment
            {
                Targets = new List<IAssignable> { expr },
                Values = new List<IExpression> { func_def }
            };
        }

        public Repeat ReadRepeat()
        {
            if (!CurToken.IsKeyword("repeat")) ThrowExpect("repeat statement", CurToken);
            Move();
            SkipSemicolons();
            var statements = new List<IStatement>();
            while (!CurToken.IsKeyword("until") && !CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            if (!CurToken.IsKeyword("until")) ThrowExpect("'until' keyword", CurToken);
            Move();

            var cond = ReadExpression();

            return new Repeat
            {
                Condition = cond,
                Block = new Block { Statements = statements }
            };
        }

        public Block ReadBlock(bool alone = false)
        {
            if (!CurToken.IsKeyword("do")) ThrowExpect("block", CurToken);
            Move();
            SkipSemicolons();

            var statements = new List<IStatement>();
            while (!CurToken.IsKeyword("end") && !CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            Move();

            return new Block { Statements = statements };
        }

        public GenericFor ReadGenericFor()
        {
            if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);

            var var_names = new List<string> { CurToken.Value };
            Move();

            while (CurToken.IsPunctuation(","))
            {
                Move();
                if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);
                var_names.Add(CurToken.Value);
                Move();
            }

            if (!CurToken.IsKeyword("in")) ThrowExpect("'in' keyword", CurToken);
            Move();

            var iterator = ReadExpression();
            var block = ReadBlock();

            return new GenericFor
            {
                VariableNames = var_names,
                Iterator = iterator,
                Block = block
            };
        }

        public NumericFor ReadNumericFor()
        {
            if (CurToken.Type != TokenType.Identifier) ThrowExpect("identifier", CurToken);

            var var_name = CurToken.Value;
            Move();

            if (!CurToken.IsPunctuation("=")) ThrowExpect("assignment", CurToken);
            Move();

            var start_pos = ReadExpression();
            if (!CurToken.IsPunctuation(",")) ThrowExpect("end point expression", CurToken);
            Move();
            var end_pos = ReadExpression();

            IExpression step = null;
            if (CurToken.IsPunctuation(","))
            {
                Move();
                step = ReadExpression();
            }

            if (step == null && ParserSettings.AutofillNumericForStep)
            {
                step = new NumberLiteral { Value = 1 };
            }

            var block = ReadBlock();

            return new NumericFor
            {
                VariableName = var_name,
                StartPoint = start_pos,
                EndPoint = end_pos,
                Step = step,
                Block = block
            };
        }

        public For ReadFor()
        {
            if (!CurToken.IsKeyword("for")) ThrowExpect("for statement", CurToken);

            Move();

            var peek = PeekToken;
            if (peek.IsPunctuation(",") || peek.IsKeyword("in"))
            {
                return ReadGenericFor();
            }
            else
            {
                return ReadNumericFor();
            }
        }

        #region PLOOP [https: //github.com/kurapica/PLoop/blob/master/README-zh.md]

        /// <summary>
        /// PLoop(function(_ENV)
        /// </summary>
        public Ploop ReadPloop()
        {
            if (!CurToken.IsKeyword("PLoop")) ThrowExpect("Module statement", CurToken);
            Move();
            if (!CurToken.IsPunctuation("(")) ThrowExpect("(", CurToken);
            Move();
            if (!CurToken.IsKeyword("function")) ThrowExpect("function", CurToken);

            //skip (_ENV) 
            Move();
            Move();
            Move();
            Move();

            //read module statements
            var Statements = new List<IStatement>();
            while (!CurToken.IsEOF())
            {
                if (CurToken.IsKeyword("end") && PeekToken.IsPunctuation(")"))
                {
                    //skip module
                    Move();
                    break;
                }
            
                var statement = ReadStatement();
                Statements.Add(statement);
            }
            
            Move();
            
        
            return new Ploop()
            {
                Statements = Statements,
            };
        }
        
        
        /// <summary>
        /// reads ploop module
        /// </summary>
        public PloopModule ReadPloopModule()
        {
            if (!CurToken.IsKeyword("Module")) ThrowExpect("Module statement", CurToken);
            Move();
            var moduleName = ReadStringLiteral().Value;
            if (!CurToken.IsPunctuation("(")) ThrowExpect("(", CurToken);
            Move();
            if (!CurToken.IsKeyword("function")) ThrowExpect("function", CurToken);

            //skip (_ENV) 
            Move();
            Move();
            Move();
            Move();

            //read module statements
            var Statements = new List<IStatement>();
            while (!CurToken.IsEOF())
            {
                if (CurToken.IsKeyword("end") && PeekToken.IsPunctuation(")"))
                {
                    //skip module
                    Move();
                    break;
                }

                var statement = ReadStatement();
                Statements.Add(statement);
            }

            Move();

            //filter the namespace  and import functions
            FunctionCall _namespaceCall = null;
            List<FunctionCall> _importFunctions = new List<FunctionCall>();
            foreach (var statement in Statements)
            {
                if (statement is FunctionCall _functionCall) 
                {
                    if (_functionCall.Function is Variable _variable)
                    {
                        if (_variable.Name == "namespace")
                        {
                            _namespaceCall = _functionCall;
                        }
                        else if (_variable.Name == "import")
                        {
                            _importFunctions.Add(_functionCall);
                        }
                    }
                }
            }

            if (_namespaceCall != null)
                Statements.Remove(_namespaceCall);
            foreach (var importFunction in _importFunctions)
            {
                Statements.Remove(importFunction);    
            }
            
            return new PloopModule()
            {
                ModuleName = moduleName,
                Statements = Statements,
                NamespaceFunction = _namespaceCall,
                ImportFunctions = _importFunctions,
            };
        }


        /// <summary>
        /// read ploop class 
        /// </summary>
        /// <returns></returns>
        public PloopClass ReadPloopClass()
        {
            if (!CurToken.IsKeyword("class")) ThrowExpect("ploop class statement", CurToken);
            Move();
            var className = ReadStringLiteral().Value;
            if (!CurToken.IsPunctuation("(")) ThrowExpect("(", CurToken);
            Move();
            if (!CurToken.IsKeyword("function")) ThrowExpect("function", CurToken);

            //skip (_ENV)
            Move();
            Move();
            Move();
            Move();

            var inheritClass = default(string);
            if (CurToken.IsKeyword("inherit"))
            {
                Move();
                inheritClass = ReadStringLiteral().Value;
                //CUSTOM 
                if (inheritClass == "ViewBase")
                {
                    inheritClass = "ViewBaseN";
                }
            }

            var Statements = new List<IStatement>();
            while (!CurToken.IsEOF())
            {
                if (CurToken.IsKeyword("end") && PeekToken.IsPunctuation(")"))
                {
                    //skip class 
                    Move();
                    break;
                }

                var statement = ReadStatement();
                Statements.Add(statement);
            }

            Move();

            return new PloopClass()
            {
                ClassName = className,
                InheritClassName = inheritClass,
                Statements = Statements,
            };
        }

        /// <summary>
        /// read ploop class property 
        /// property "Name" { field = "__name", type = System.String, set = "unknown", set = false}
        /// </summary>
        /// <returns></returns>
        public PloopProperty ReadPloopProperty()
        {
            if (!CurToken.IsKeyword("property")) ThrowExpect("property statement", CurToken);
            Move();
            var propertyName = ReadStringLiteral().Value;
            if (!CurToken.IsPunctuation("{")) ThrowExpect("{", CurToken);

            var propertyStruct = ReadTableConstructor();

            return new PloopProperty()
            {
                PropertyName = propertyName,
                PropertyTable = propertyStruct,
            };
        }

        /// <summary>
        /// read enum
        /// enum "name" { -- key-value pairs }
        /// enum "Direction" { North = 1, East = 2, South = 3, West = 4 }
        /// </summary>
        /// <returns></returns>
        public PloopEnum ReadPloopEnum()
        {
            if (!CurToken.IsKeyword("enum")) ThrowExpect("property statement", CurToken);
            Move();
            var enumName = ReadStringLiteral().Value;
            if (!CurToken.IsPunctuation("{")) ThrowExpect("{", CurToken);

            var enumStruct = ReadTableConstructor();

            return new PloopEnum()
            {
                EnumName = enumName,
                enumStruct = enumStruct,
            };
        }

        /// <summary>
        /// read ploop atrbibute
        /// </summary>
        /// <returns></returns>
        public PloopAttribute ReadPloopAttribute()
        { 
            if(!Regex.IsMatch(CurToken.Value, @"^__[_a-zA-Z][_a-zA-Z0-9]*__$")) ThrowExpect("attribte statement", CurToken);
            var expression = ReadExpression();
            Debug.Assert(expression is FunctionCall,"expression is FunctionCall");
            return new PloopAttribute()
            {
                FunctionCall = expression as FunctionCall,
            };
        }
        #endregion

        /// <summary>
        /// 读取主语句
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IStatement ReadPrimaryStatement()
        {
            if (CurToken.IsKeyword("break"))
            {
                return ReadBreak();
            }

            if (CurToken.IsKeyword("return"))
            {
                return ReadReturn();
            }

            if (CurToken.IsKeyword("if"))
            {
                return ReadIf();
            }

            if (CurToken.IsKeyword("while"))
            {
                return ReadWhile();
            }

            if (CurToken.IsKeyword("function"))
            {
                return ReadNamedFunctionDefinition();
            }

            if (CurToken.IsKeyword("repeat"))
            {
                return ReadRepeat();
            }

            if (CurToken.IsKeyword("for"))
            {
                return ReadFor();
            }

            if (CurToken.IsKeyword("do"))
            {
                return ReadBlock(alone: true);
            }

            if (CurToken.IsKeyword("local"))
            {
                Move();
                if (CurToken.IsKeyword("function"))
                {
                    var local_assign = ReadNamedFunctionDefinition();
                    local_assign.IsLocal = true;
                    return local_assign;
                }
                else
                {
                    var local_expr_token = CurToken;
                    var local_expr = ReadExpression();
                    var local_assign = TryReadFullAssignment(true, local_expr, local_expr_token);
                    if (local_assign == null) ThrowExpect("assignment statement", CurToken);
                    local_assign.IsLocal = true;
                    return local_assign;
                }
            }

            if (CurToken.IsKeyword("PLoop"))
            {
                return ReadPloop();
            }
            
            if (CurToken.IsKeyword("Module"))
            {
                return ReadPloopModule();
            }

            if (CurToken.IsKeyword("class"))
            {
                return ReadPloopClass();
            }

            if (CurToken.IsKeyword("property"))
            {
                return ReadPloopProperty();
            }

            if (CurToken.IsKeyword("enum"))
            {
                return ReadPloopEnum();
            }

            if (CurToken.Type == TokenType.Identifier && Regex.IsMatch(CurToken.Value, @"^__[_a-zA-Z][_a-zA-Z0-9]*__$"))
            {
                return ReadPloopAttribute();
            }


            var expr_token = CurToken;
            var expr = ReadExpression();
            var assign = TryReadFullAssignment(false, expr, expr_token);
            if (assign != null) return assign;

            if (expr is FunctionCall)
            {
                return expr as FunctionCall;
            }

            ThrowExpect("statement", expr_token);
            throw new Exception("unreachable");
        }

        /// <summary>
        /// Reads a single statement.
        /// </summary>
        /// <returns>The statement.</returns>
        public IStatement ReadStatement()
        {
            var stat = ReadPrimaryStatement();
            SkipSemicolons();
            return stat;
        }

        /// <summary>
        /// Reads a list of statements.
        /// </summary>
        /// <returns>`Block` node (`TopLevel` = `true`).</returns>
        public Block Read()
        {
            var statements = new List<IStatement>();

            while (!CurToken.IsEOF())
            {
                statements.Add(ReadStatement());
            }

            return new Block { Statements = statements, TopLevel = true };
        }

        public Block ReadAndPostProcess()
        {
            var block = Read();
            
            //1. process partial class 
            
            
            
            
            
            
            
            
            return block;
        }
    }
}