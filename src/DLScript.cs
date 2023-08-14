using System;
using static System.Console;
using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace DickLang {
    class Compiler {
        protected internal static Stopwatch Timer = new Stopwatch();
        private protected static int LineNumber = 0;
        private protected static string[] CurrentCode;
        private protected static KeyValuePair<int, int> LoopInfo = new(-1, -1);
        private protected static Dictionary<string, object> FunctionInfo = new() {
            { "Name",  null },
            { "Start", -1 },
            { "End", -1 },
        };
        private protected static Dictionary<string, object> FutureErrorLine = new(){
            { "Line", -1 },
            { "Error", Array.Empty<object>() }
        };

        private static void Main(string[] args) {
            Init();

            WriteLine("DLScript Interpreter\n(c) WAP Industries. All Rights reserved.\n");
            while (true) {
                ResetCompiler();
                Write(">>> ");
                string input = ReadLine().Trim();
                if (input == "__quit__") return;
                else {
                    string[] Lines;
                    if (input.Trim().Length == 0) {
                        WriteLine("No file provided");
                        continue;
                    }
                    try { Lines = CurrentCode = File.ReadAllLines(input); }
                    catch {
                        WriteLine($"Invalid file path: {input}");
                        continue;
                    }

                    Timer.Reset();
                    Timer.Start();
                    while (LineNumber < Lines.Length) {
                        if (LineNumber < 0 || LineNumber >= Lines.Length) break;

                        // future error
                        if (Lines[LineNumber].Trim().Length == 0 || Lines[LineNumber].Trim()[0]=='#') { LineNumber++; continue; }
                        if (Convert.ToInt32(FutureErrorLine?["Line"]) != -1) {
                            if (Convert.ToInt32(LineNumber) == Convert.ToInt32(FutureErrorLine["Line"])) {
                                string[] err = (string[])FutureErrorLine["Error"];
                                Error.RunTimeError(err[0], err[1]);
                                break;
                            }
                        }

                        if (!Run(Lines[LineNumber])) break;
                        if (LineNumber == LoopInfo.Value) {
                            LineNumber = LoopInfo.Key;
                            continue;
                        }
                        if (LineNumber == (int)FunctionInfo["End"])
                            Keywords.EscFunc();
                        LineNumber++;
                    }
                    DisplayRunTime();
                }
            }
        }

        protected internal static void DisplayRunTime() {
            Timer.Stop();
            Console.WriteLine("\n--------------------------------");
            Console.WriteLine($"Process exited after {(decimal)(Timer.ElapsedMilliseconds) / 1000} seconds\n");
        }
        
        private static bool Run(string Line) {
            var Tokens = Parser.Parse(Line);
            if (Tokens == null) return false;

            if (Interpreter.Interprete((string[])Tokens)==null) return false;
            return true;
        }

        private static void Init() {
            // expr parser tends to be slow on the 1st run
            Interpreter.Interprete(new string[] { "print", "nigger" });
            Clear();
        }
        private static void ResetCompiler() {
            LineNumber = 0;
            Keywords.Variables = new();
            Keywords.Methods = new();
            LoopInfo = new(-1, -1);
            FunctionInfo = new() {
                { "Name",  null},
                { "Start", -1 },
                { "End", -1 },
            };
            FutureErrorLine = new(){
                { "Line", -1 },
                { "Error", Array.Empty<object>() }
            };
        }
    }
}