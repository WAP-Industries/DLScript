using System;
using static System.Console;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Reflection;

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
                        if (LoopInfo.Count() > 0 && LineNumber == LoopInfo[^1].Value) {
                            LineNumber = LoopInfo[^1].Key;
                            continue;
                        }
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
            string? GetDir() {
                var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

                while (directoryInfo != null) {
                    if (directoryInfo.GetFiles("*.sln").Length > 0)
                        return @$"{directoryInfo.FullName}\DLScript";
                    directoryInfo = directoryInfo.Parent;
                }
                return null;
            }

            bool CheckAssoc() {
                WriteLine("Checking extension assocations...");
                bool? Result = null;

                string? CurrDir = GetDir();
                if (CurrDir == null) return false;

                Process process = new Process();
                process.StartInfo = new ProcessStartInfo {
                    FileName = $@"{CurrDir}\checkext.bat",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                process.OutputDataReceived += (sender, e) => 
                    Result = Result==null ? Convert.ToBoolean(e.Data) : Result;
                
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                
                Clear();
                return Convert.ToBoolean(Result);
            }

            if (!CheckAssoc()) {
                // associate dlscript logo
                WriteLine("Setting icons...");
                try {
                    string? CurrDir = GetDir();
                    if (CurrDir == null) throw new Exception();

                    Process.Start(
                        new ProcessStartInfo {
                            FileName = @$"{CurrDir}\assoc.bat",
                            Verb = "runas",
                            UseShellExecute = true
                        }
                    );

                } catch {
                    Clear();
                    WriteLine("Failed to set file icons");
                }
            }
            
            // expr parser tends to be slow on the 1st run
            Interpreter.Interprete(new string[] { "print", "Setting up..." });
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