using System;
using static System.Console;
using System.Collections;
using System.Collections.Generic;
using static DickLang.Compiler;
using static System.Text.Json.JsonSerializer;
using DickLang;
using System.Text.RegularExpressions;

struct TokenInfo {
    public string[] Pattern, Args;
    public TokenInfo(string[] Tokens, string[] Argtypes = null) {
        this.Args = Argtypes ?? Array.Empty<string>();
        this.Pattern = Tokens;
    }
}

struct Keyword {
    public TokenInfo Info;
    public Func<object[], object> Func;

    public Keyword(TokenInfo Info, Func<object[], object> Func) {
        this.Info = Info;
        this.Func = Func;
    }
}

class Keywords : DickLang.Compiler {
    protected internal static readonly string[] DataTypes = {
        "string", "number", "bool",
        "string[]", "number[]", "bool[]",
    };
    protected internal static readonly Regex Symbols = new Regex("[+-/*()<>!=&|\uF480\uF481]");
    protected internal static readonly string[] Blocks = { "if", "while", "class" };
    protected internal static readonly string[] Conditionals = { "if", "while" };
    protected internal static readonly string[] NameFunctions = { "call", "function" };

    protected internal static Dictionary<string, Dictionary<string, object>> Variables = new();
    protected internal static Dictionary<string, Dictionary<string, object>> Methods = new();
    protected internal static Dictionary<string, object> Classes = new();

    protected internal static Dictionary<string, Keyword> Functions = new()
    {
        {
            "print", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"string"}),
                (parameters)=> {
                    Console.Write(parameters[0]);
                    return true;
                }
            )
        },
        {
            "abort", new Keyword(
                new TokenInfo(new string[]{"keyword"}),
                (parameters) => {
                    LineNumber = CurrentCode.Length;
                    return true;
                }
            )
        },
        {
            "delete", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"rawstring"}),
                (parameters) => {
                    if (!Keywords.Variables.ContainsKey(parameters[0] as string))
                        return Error.RunTimeError("Reference", $"Variable {parameters[0]} does not exist");
                    Keywords.Variables.Remove(parameters[0] as string);
                    return true;
                }
             )
        },
        {
            "if", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"bool", "number"}),
                (parameters)=>{
                    if (!CheckBlockSpan(parameters[1], "if")) return null;
                    SetFutureError(parameters[1]);
                    if (Serialize(parameters[0])=="false")
                        LineNumber += (int) parameters[1];
                    return true;
                }
            )
        },
        {
            "while",new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"bool", "number"}),
                (parameters) =>
                {
                    if (!CheckBlockSpan(parameters[1], "while")) return null;
                    SetFutureError(parameters[1]);
                    if (Serialize(parameters[0]) == "false")
                    {
                        LineNumber = LoopInfo.Value;
                        LoopInfo = new KeyValuePair<int, int>(-1, -1);
                        return true;
                    }
                    LoopInfo = new KeyValuePair<int, int>(LineNumber, LineNumber+(int)parameters[1]);
                    return true;
                }
            )
        },
        {
            "break", new Keyword(
                new TokenInfo(new string[]{"keyword"}),
                (parameters) =>
                {
                    if (InLoop("break")){
                        LoopInfo = new KeyValuePair<int, int>(-1, -1);
                        return true;
                    }
                    return null;
                }
            )
        },
        {
            "continue", new Keyword(
                new TokenInfo(new string[]{"keyword"}),
                (parameters) =>
                {
                    if (InLoop("continue")){
                        LineNumber = LoopInfo.Key;
                        return true;
                    }
                    return null;
                }
             )
        },
        {
            "function", new Keyword(
                new TokenInfo(new string[]{"function", "name", "funcargs"}),
                (parameters) => {
                    string[] Args = Parser.SplitArgs(parameters[1] as string);

                    Dictionary<string, object> FuncArgs = new();
                    foreach(var Arg in Args.SkipLast(1).ToArray()) {
                        int sep = Arg.IndexOf(':');
                        string argname = Arg.Substring(0, sep).Trim();
                        string argtype = Arg.Substring(sep + 1, Arg.Length-sep-1).Trim();
                        FuncArgs.Add(argname, new Dictionary<string, object>() {
                            {"Type", argtype},
                            {"Value", "null" }
                        });
                    }
                    if (Keywords.Methods.ContainsKey(parameters[0] as string))
                        Keywords.Methods.Remove(parameters[0] as string);
                    Methods.Add(
                        parameters[0] as string,
                        new Dictionary<string, object>() {
                            { "Lines", new int[]{LineNumber, LineNumber + Convert.ToInt32(Args[^1]) } },
                            { "Arguments", FuncArgs }
                        }
                    );
                    LineNumber+=Convert.ToInt32(Args[^1]);
                    return true;
                }
            )
        },
        {
            "call", new Keyword(
                new TokenInfo(new string[]{"keyword", "name", "funcargs"}, new string[]{"rawstring", "*"}),
                (parameters) => {
                    if (!Methods.ContainsKey(parameters[0] as string))
                        return Error.RunTimeError("Reference", $"Function {parameters[0]} does not exist");

                    var FunctionArgs = Keywords.Methods[parameters[0] as string]["Arguments"] as Dictionary<string, object>;

                    string[] PassedArgs = (Keywords.Methods[parameters[0] as string]["Arguments"] as Dictionary<string, object>).Keys.ToArray();

                    // check and assign args
                    string[] Tokens = new string[]{"call", parameters[0] as string, parameters[1] as string };
                    object ArgsList = CheckCallParams(FunctionArgs, parameters[1] as string, Tokens);
                    if (parameters.Length == 2 && ArgsList==null)
                        return null;
                    for(int i=0; i<FunctionArgs.Keys.Count();i++) {
                        var _ = Parser.SplitArgs(parameters[1] as string);
                        var line = new string[]{"call", parameters[0] as string, parameters[1] as string };
                        string type = (string) (FunctionArgs[FunctionArgs.Keys.ElementAt(i)] as Dictionary<string, object>)["Type"];

                        object value;
                        if (type.Contains("[]")) {
                            value = Lexer.EvalExpr($"~[...{_[i]}]~", Tokens, true, Convert.ToString(type).Replace("[]", ""));
                            value = Parser.SetArrayElems(new string[]{type, "nig", Convert.ToString(value)});
                        }
                        else
                            value = Lexer.EvalExpr(_[i], line, (ArgsList as string[])[i]=="string", "");

                        (Methods[parameters[0] as string]["Arguments"] as Dictionary<string, object>)[(PassedArgs)[i]] = new Dictionary<string, object>(){
                            {"Type", type},
                            {"ArrayType", type.Contains("[]") ? type.Replace("[]", "") :null },
                            {"Value",  value}
                        };
                    }

                    var lines = ((int[])(Methods[parameters[0] as string]["Lines"]));
                    FunctionInfo["Start"] = (object) LineNumber;
                    FunctionInfo["End"] = (object) lines[1];
                    FunctionInfo["Name"] = parameters[0] as string;
                    LineNumber = lines[0];
                    return true;
                }
            )
        },
        {
            "return", new Keyword(
                new TokenInfo(new string[]{"keyword"}),
                (parameters) => {
                    if ((int) FunctionInfo["Start"]==-1)
                        return Error.RunTimeError("Syntax", "Illegal return statement");
                    EscFunc();
                    return true;
                }
            )
        },
        {
            "sleep", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"number"}),
                (parameters) => {
                    Thread.Sleep((int) parameters[0]);
                    return true;
                }
            )
        }
    };

    protected internal static void EscFunc() {
        LineNumber = (int)FunctionInfo["Start"];
        FunctionInfo["Start"] = FunctionInfo["End"] = -1;
        FunctionInfo["Name"] = null;
    }

    protected internal static bool CheckBlockSpan(object line, string blocktype) {
        string b = $"{char.ToUpper(blocktype[0])}{blocktype[1..]}";
        if (Convert.ToDouble(line) <= 0 || !Int32.TryParse(Convert.ToString(line), out _)) {
            Error.RunTimeError("Syntax", $"{b} block must span valid number of lines");
            return false;
        }
        if (LineNumber + Convert.ToDouble(line) >= CurrentCode.Length) {
            Error.RunTimeError("Syntax", $"{b} block extends past end of file");
            return false;
        }
        return true;
    }

    private static object CheckCallParams(Dictionary<string, object> StoredArgs, string FuncArgs, string[] Tokens) {
        List<string> ArgsList = new();
        List<string> ValuesList = new();

        if (ArgsList.Count() == 0) {
            if (FuncArgs==null) 
                return Array.Empty<object>();
            else
                return Error.RunTimeError("Syntax", 
                    $"Function {Tokens[1]} expects {ArgsList.Count()} arguments, {Parser.SplitArgs(FuncArgs).Length} supplied");
        }
        string[] RawArgs = Parser.SplitArgs(FuncArgs);


        foreach (var key in StoredArgs)
            ArgsList.Add((key.Value as Dictionary<string, object>)["Type"] as string);
        foreach (var arg in RawArgs) {
            var argtype = (StoredArgs[StoredArgs.Keys.ToArray().Where(i => Array.IndexOf(StoredArgs.Keys.ToArray(), i) == Array.IndexOf(RawArgs, arg)).ToArray()[0]] as Dictionary<string, object>)["Type"];
            object val;
            if (Convert.ToString(argtype).Contains("[]"))
                val = Lexer.EvalExpr($"~[...{arg}]~", Tokens, true, Convert.ToString(argtype).Replace("[]", ""));
            else 
                val = Lexer.EvalExpr(arg, Tokens, Convert.ToString(argtype) == "string", Convert.ToString(argtype));
            if (val == null) return null;
            ValuesList.Add(Convert.ToString(val));
        }
        if (Parser.CheckArguments(String.Join("<>", ValuesList.ToArray()), ArgsList.ToArray()) == null)
            return null;

        return ArgsList.ToArray();
    }

    private static bool InLoop(string name) {
        return LoopInfo.Key == -1 ?
            Convert.ToBoolean(Error.RunTimeError("Syntax", $"Illegal {name} statement")) : true;
    }

    private static void SetFutureError(object line) {
        string[] lastline = CurrentCode[LineNumber + Convert.ToInt32(line)].Split("8=D").Select(i => i.Trim()).ToArray();
        if (Blocks.Contains(lastline[0])) {
            Compiler.FutureErrorLine["Line"] = LineNumber + Convert.ToInt32(line);
            Compiler.FutureErrorLine["Error"] = new string[] { "Syntax", $"Unclosed {lastline[0]} block" };
        }
    }
}