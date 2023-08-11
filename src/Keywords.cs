﻿using System;
using static System.Console;
using static DickLang.Compiler;
using static System.Text.Json.JsonSerializer;
using DickLang;
using System.Text.RegularExpressions;
using System.Data;

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
        "string", "number", "bool", "object",
        "string[]", "number[]", "bool[]"
    };
    protected internal static readonly Dictionary<string, object> DefaultValues = new(){
        {"string", "" },
        {"number", 0 },
        {"bool", true},
        {"object", "{..}"},
        {"array", Array.Empty<object>()}
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
                            {"ArrayType", "null"},
                            {"Properties", "null"},
                            {"Value", DefaultValues[argtype.Contains("[]") ? "array": argtype]}
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
                    object ArgsList = CheckCallParams(FunctionArgs, Convert.ToString(parameters[1]), Tokens);
                    if (parameters.Length == 2 && ArgsList==null)
                        return null;
                    for(int i=0; i<FunctionArgs.Keys.Count();i++) {
                        var _ = Parser.SplitArgs(parameters[1] as string);
                        var line = new string[]{"call", parameters[0] as string, parameters[1] as string };
                        string type = (string) (FunctionArgs[FunctionArgs.Keys.ElementAt(i)] as Dictionary<string, object>)["Type"];

                        object value;
                        if (type.Contains("[]")) {
                            value = Lexer.EvalExpr(type.Contains("string") ? $"{_[i]}" : $"~[{_[i]}]~",
                                Tokens, type.Contains("string"), Convert.ToString(type).Replace("[]", ""));
                            value = Parser.SetArrayElems(new string[]{type, "nig", Convert.ToString(value)});
                        }
                        else if (type == "object")
                            value = Lexer.EvalExpr(_[i], Tokens, false, "object");
                        else
                            value = Lexer.EvalExpr(_[i], line, (ArgsList as string[])[i]=="string", "");

                        (Methods[parameters[0] as string]["Arguments"] as Dictionary<string, object>)[(PassedArgs)[i]] = new Dictionary<string, object>(){
                            {"Type", type},
                            {"ArrayType", type.Contains("[]") ? type.Replace("[]", "") :null },
                            {"Properties", null },
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
        },
        {
            "set", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"string", "number", "string"}),
                (parameters) => Convert.ToBoolean(ModifyArray(parameters[0], parameters[1], parameters[2], "set"))
             )
        },
        {
            "insert", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[] {"string", "number", "string"}),
                (parameters) => Convert.ToBoolean(ModifyArray(parameters[0], parameters[1], parameters[2], "insert"))
            )
        },
        {
            "append", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"string", "string"}),
                (parameters) => Convert.ToBoolean(ModifyArray(parameters[0], null, parameters[1], "append"))
             )
        },
        {
            "remove", new Keyword(
                new TokenInfo(new string[]{"keyword", "args"}, new string[]{"string", "number"}),
                (parameters) => Convert.ToBoolean(ModifyArray(parameters[0], parameters[1], null, "remove"))
            )
        }
    };

    private static object ModifyArray(object _name, object _index, object _value, string mode) {
        string rawname = (_name as string).Trim();
        string name = rawname.Substring(1);
        // check name
        if (new char[] { '%', '$' }.Select(i => rawname[0]==i).ToArray().Length == 0)
            return Error.RunTimeError("Syntax", "Array provided must be variable");
        if (rawname[0] == '%' && FunctionInfo["Name"] == null)
            return Error.RunTimeError("Syntax", "Cannot reference function arguments outside function block");

        var Coll = Deserialize<Dictionary<string, object>>(Serialize(
            (rawname[0]=='$' ? Keywords.Variables : Keywords.Methods[FunctionInfo["Name"] as string]["Arguments"])));
        if (!Coll.Keys.Contains(name))
            return Error.RunTimeError("Reference", $"{(rawname[0] == '$' ? "Variable" : "Function argument")} {name} does not exist");

        object[] array; 
        Dictionary<string, object> variable;
        try {
            variable = Deserialize<Dictionary<string, object>>(Serialize(Coll[name]));
            array = Deserialize<object[]>(Serialize(variable["Value"]));
        } 
        catch {
            return Error.RunTimeError("Reference", "Variable provided must be array");
        }

        // check index
        object index=null;
        if (mode != "append") {
            index = Lexer.EvalExpr(Convert.ToString(_index), Array.Empty<string>(), false, "number", false);
            if (index == null) 
                return Error.RunTimeError("Reference", $"Index must be an integer");
            if (Convert.ToInt64(index)<0 || Convert.ToInt64(index)>array.Length)
                return Error.RunTimeError("Reference", $"Index {index} is out of bounds");
        }

        // check value
        string arrtype = Convert.ToString(variable["ArrayType"]);

        object value = null;
        if (mode != "remove") {
            value = Lexer.EvalExpr(arrtype == "string" ? $"~[{Convert.ToString(_value)}]~" : Convert.ToString(_value),
                        new string[] { "set" }, arrtype == "string", arrtype);
            if (value == null) return null;
        }

        var list = array.ToList<object>(); 
        switch (mode) {
            case "set":
               array[Convert.ToInt64(index)] = value;
                break;
            case "append":
                list.Add(value);
                array = list.ToArray();
                break;
            case "remove":
                list.RemoveAt(Convert.ToInt32(index));
                array = list.ToArray();
                break;
            case "insert":
                list.Insert(Convert.ToInt32(index), value);
                array = list.ToArray();
                break;
        }

        var tempdic = Deserialize<Dictionary<string, Dictionary<string, object>>>(Serialize(rawname[0] == '$' ? Variables :
            Methods[Convert.ToString(FunctionInfo["Name"])]["Arguments"]));
        tempdic[name] = new Dictionary<string, object>(){
            { "Type", variable["Type"] },
            { "ArrayType", variable["ArrayType"] },
            { "Value",  array}
        };
        if (rawname[0] == '$')
            Keywords.Variables = tempdic;
        else
            Keywords.Methods[Convert.ToString(FunctionInfo["Name"])]["Arguments"] = tempdic;
        return true;
    }

    protected internal static void EscFunc() {
        LineNumber = (int) FunctionInfo["Start"];
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
        
        string[] RawArgs = Parser.SplitArgs(FuncArgs);

        foreach (var key in StoredArgs)
            ArgsList.Add((key.Value as Dictionary<string, object>)["Type"] as string);
        foreach (var arg in RawArgs) {
            var argtype = (StoredArgs[StoredArgs.Keys.ToArray().Where(i => Array.IndexOf(StoredArgs.Keys.ToArray(), i) == Array.IndexOf(RawArgs, arg)).ToArray()[0]] as Dictionary<string, object>)["Type"];
            object val;
            if (Convert.ToString(argtype).Contains("[]"))
                val = Lexer.EvalExpr((argtype as string).Contains("string") ? $"{arg}" : $"~[{arg}]~", 
                    Tokens, (argtype as string).Contains("string"), Convert.ToString(argtype).Replace("[]", ""));
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

    protected internal static Dictionary<string, Dictionary<string, object>> CreateObject(string Properties) {
        Dictionary<string, Dictionary<string, object>> NewObject = new();

        foreach(string prop in Properties.Split("<>")){
            int sep = prop.IndexOf(":");
            string name = prop.Substring(0, sep).Trim();
            string type = prop.Substring(sep + 1).Trim();
            NewObject.Add(
                name, new() {
                    { "Type", type },
                    { "ArrayType", type.Contains("[]") ? type.Replace("[]", "") : null },
                    { "Value", DefaultValues[type.Contains("[]") ? "array":type] }
                }
            );
        }
        return NewObject;
    }
}