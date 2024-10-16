﻿using System;
using System.Collections.Generic;
using System.IO;
using Relua.Script;

namespace Relua.PowerTool {
    public static class Program {
        public class Options {
            public bool Help;
            public bool Verbose;
        }

        public static void PrintHelp() {
            Console.WriteLine($"Relua PowerTool");
            Console.WriteLine($"Subcommands: ");
            Console.WriteLine($"- help");
            Console.WriteLine($"- expr");
            Console.WriteLine($"- parse");
            Console.WriteLine($"- script");
            Console.WriteLine();
            Console.WriteLine($"Use --help on the above to display more information.");
        }

        public static void PrintExprHelp() {
            Console.WriteLine($"Relua PowerTool");
            Console.WriteLine($"  expr EXPR");
            Console.WriteLine($"Parses an expression and writes it back to stdout.");
        }

        public static void PrintParseHelp() {
            Console.WriteLine($"Relua PowerTool");
            Console.WriteLine($"  parse FILE");
            Console.WriteLine($"Parses a script and writes it back to stdout.");
        }

        public static void PrintScriptHelp() {
            Console.WriteLine($"Relua PowerTool");
            Console.WriteLine($"  script FILE SCRIPT");
            Console.WriteLine($"Transforms the file with the provided Relua.Script script and writes it back to stdout.");
        }

        public static int ParseMain(Options opts, List<string> args) {
            if (opts.Help) {
                PrintParseHelp();
                return 0;
            }

            if (args.Count == 0) {
                Console.WriteLine("Not enough arguments.");
                PrintParseHelp();
                return 1;
            }

            try {
                var parser = new Parser(File.ReadAllText(args[0]));
                Console.WriteLine(parser.Read());
            } catch (Exception e) {
                Console.WriteLine($"An error occured while parsing/writing the file:");
                Console.WriteLine(e.Message);
                if (opts.Verbose) Console.WriteLine(e.StackTrace);
                return 1;
            }

            return 0;
        }

        public static int ExprMain(Options opts, List<string> args) {
            if (opts.Help) {
                PrintExprHelp();
                return 0;
            }

            if (args.Count == 0) {
                Console.WriteLine("Not enough arguments.");
                PrintExprHelp();
                return 1;
            }

            var s = string.Join(" ", args);

            try {
                var parser = new Parser(s);
                Console.WriteLine(parser.ReadExpression());
            } catch (Exception e) {
                Console.WriteLine($"An error occured while parsing/writing the file:");
                Console.WriteLine(e.Message);
                if (opts.Verbose) Console.WriteLine(e.StackTrace);
                return 1;
            }

            return 0;
        }

        public static int ScriptMain(Options opts, List<string> args) {
            if (opts.Help) {
                PrintScriptHelp();
                return 0;
            }

            if (args.Count < 2) {
                Console.WriteLine("Not enough arguments.");
                PrintScriptHelp();
                return 1;
            }

            try {
                var runtime = new XTRuntime.XTRuntime();
                var visitor = new LuaVisitor(runtime);
                runtime.DoFile(args[1]);

                var parser = new Parser(File.ReadAllText(args[0]));
                var block = parser.Read();

                visitor.Visit(block);

                Console.WriteLine(block);
            } catch (Exception e) {
                Console.WriteLine($"An error occured while transforming the script:");
                Console.WriteLine(e.Message);
                if (opts.Verbose) Console.WriteLine(e.StackTrace);
                return 1;
            }

            return 0;
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("----");
            var testluafile = @"f:/inputfile.lua";
            var tokenizer = new Tokenizer(File.ReadAllText(testluafile));
            var parser = new Parser(tokenizer);
            var expr = parser.Read();
            Console.WriteLine($"{expr}");
            var outpath = "f:/outputfile.lua";
            File.WriteAllText(outpath,expr.ToString());
            Console.WriteLine(outpath);
            return default;
        }
    }
}
