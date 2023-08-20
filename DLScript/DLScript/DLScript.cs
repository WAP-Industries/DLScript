using System;
using static System.Console;
using System.Diagnostics;

namespace DickLang {
    class Compiler {
        private static readonly string StartUpMsg = "DLScript Interpreter\n(c) WAP Industries. All Rights reserved.\n";
        protected internal static Stopwatch Timer = new Stopwatch();
        private protected static int LineNumber = 0;
        private protected static string[] CurrentCode;
        private protected static List<KeyValuePair<int, int>> LoopInfo = new();
        private protected static Dictionary<string, object> FunctionInfo = new() {
            { "Name",  null },
            { "Start", -1 },
            { "End", -1 },
        };
        private protected static Dictionary<string, object> FutureErrorLine = new(){
            { "Line", -1 },
            { "Error", Array.Empty<object>() }
        };

        // generating random numbers
        private static Random Seed = new Random();
        protected internal static double Rand() => Math.Round(Seed.NextDouble(), 10);

        private static void Main(string[] args) {
            Init();

            WriteLine(StartUpMsg);
            while (true) {
                ResetCompiler();
                Write(">>> ");

                string input = ReadLine().Trim();

                // console commands
                if (input == "__quit__")
                    return;
                else if (input == "__cls__")
                    Clear();

                // executing code files
                else {
                    string ReverseString(string str) =>
                        new string(str.ToCharArray().Reverse().ToArray());

                    string[] Lines;
                    if (input.Trim().Length == 0) {
                        WriteLine("No file provided");
                        continue;
                    }
                    try {
                        Lines = CurrentCode = File.ReadAllLines(input);

                        if (ReverseString(input).IndexOf(ReverseString(".dlscript")) != 0) {
                            WriteLine("Invalid file extension, can only run .dlscript files");
                            continue;
                        }
                    } catch {
                        WriteLine($"Invalid file path: {input}");
                        continue;
                    }

                    Timer.Reset();
                    Timer.Start();
                    while (LineNumber < Lines.Length) {
                        if (LineNumber < 0 || LineNumber >= Lines.Length) break;

                        // future error
                        if (Lines[LineNumber].Trim().Length == 0 || Lines[LineNumber].Trim()[0] == '#') { LineNumber++; continue; }
                        if (Convert.ToInt32(FutureErrorLine?["Line"]) != -1) {
                            if (Convert.ToInt32(LineNumber) == Convert.ToInt32(FutureErrorLine["Line"])) {
                                string[] err = (string[])FutureErrorLine["Error"];
                                Error.RunTimeError(err[0], err[1]);
                                break;
                            }
                        }

                        if (!Run(Lines[LineNumber])) break;
                        if (LoopInfo.Count() > 0 && LineNumber == LoopInfo[^1].Value) {
                            LineNumber = LoopInfo[^1].Key;
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
            LoopInfo = new();
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