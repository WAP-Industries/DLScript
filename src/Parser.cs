using System.Collections.Generic;
using static DickLang.Compiler;
using static System.Text.Json.JsonSerializer;
using System.Text.RegularExpressions;
using DickLang;
using System.Collections;

class Parser : DickLang.Compiler {
    private static readonly string[][] Patterns = new string[][]
    {
        new string[]{"keyword"},
        new string[]{"keyword", "args"},
        new string[]{"variable", "name", "args"},
        new string[]{"function", "name", "funcargs"},
        new string[]{"keyword", "name"},
        new string[]{"keyword", "name", "funcargs"}
    };

    protected internal static object CheckExpr(string[] Tokens) {
        int Index = (Keywords.DataTypes.Contains(Tokens[0]) ? 2 : 1);
        string[] Args = SplitArgs(Tokens[Index]);
        string[] ArgsList = (Keywords.DataTypes.Contains(Tokens[0]) ? new string[] { Tokens[0] } : Keywords.Functions[Tokens[0]].Info.Args);

        foreach (string Arg in Args) {
            if (Keywords.DataTypes.Contains(Tokens[0]) && Arg == "__input__") continue;
            if (ArgsList[Array.IndexOf(Args, Arg)] == "rawstring") continue;
            bool StrExpr = Keywords.DataTypes.Contains(Tokens[0]) ? Tokens[0] == "string" : ArgsList[Array.IndexOf(Args, Arg)] == "string";
            string LexType = Keywords.DataTypes.Contains(Tokens[0]) ? Tokens[0].Replace("[]", "") : ArgsList[Array.IndexOf(Args, Arg)];
            object Res = Lexer.EvalExpr(Arg, Tokens, StrExpr, LexType);
            if (Res == null) return null;
            Args[Array.IndexOf(Args, Arg)] = Convert.ToString(Res);
        }
        return Args;
    }

    protected internal static object Parse(string Code) {
        string[] Tokens = Code.Split("8=D").Select(i => i.Trim()).ToArray();
        string[] Pattern = FormPattern(Tokens);

        // check pattern
        if (!CheckPattern(Pattern))
            return Error.CodeError("Syntax", "Invalid Syntax");
        if (Keywords.Functions.Keys.Contains(Tokens[0])) {
            if (
                !Keywords.Functions[Tokens[0]].Info.Args.Contains("*") &&
                Serialize(Pattern) != Serialize(Keywords.Functions[Tokens[0]].Info.Pattern)
            ) return Error.CodeError("Syntax", "Invalid Syntax");
        }

        if (Tokens[0] == "function" && CheckFunction(Tokens[1], Tokens[2], Tokens) == null)
            return null;

        if (Pattern.Contains("args")) {
            if (Tokens[0].Contains("[]") && 
                Keywords.DataTypes.Contains(Tokens[0].Replace("[]", ""))
            ) {
                if (SetArrayElems(Tokens) == null)
                    return null;
            }

            else if (Tokens[0] == "object") {
                if (CheckVariable(Tokens[0], Tokens[1], Tokens[2]) == null) 
                    return null;
            }

            else {
                if (CheckExpr(Tokens) == null) return null;

                var args = (string[]) CheckExpr(Tokens);
                if (
                    Keywords.DataTypes.Contains(Tokens[0]) &&
                    CheckVariable(Tokens[0], Tokens[1], args[0]) == null
                ) return null;
                else if (
                    Keywords.Functions.ContainsKey(Tokens[0]) &&
                    CheckArguments(String.Join("<>", args), Keywords.Functions[Tokens[0]].Info.Args) == null
                ) return null;
            }
        }

        return Tokens;
    }

    protected internal static object SetArrayElems(string[] Tokens) {
        string type = Tokens[0].Replace("[]", "");
        Type arraytype = Type.GetType(type == "bool" ? "System.Boolean" : type == "number" ? "System.Decimal" : "System.String");
        IList elems = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(arraytype));

        if (Tokens[2].Trim() == "__empty__")
            return elems;

        string[] line = { type, Tokens[1], Tokens[2] };

        string[] uRawargs = Tokens[2].Split(">>").Select(i => i.Trim()).ToArray();
        List<string> rawargs = new();
        foreach (string arg in uRawargs) {
            string res = Convert.ToString(Lexer.EvalExpr(arg, line, type=="string", type));
            foreach (var elem in res.Split(">>").Select(i => i.Trim()).ToArray()) rawargs.Add(elem);
        }

        foreach (var arg in rawargs) {
            if (arg.Trim().Length == 0)
                return Error.CodeError("Syntax", "Undefined array element");
            object res = Lexer.EvalExpr(arg, line, type == "string", type);
            if (res==null) return null;
            elems.Add(Convert.ChangeType(res, arraytype));
        }
        return elems;
    }

    private static object CheckFunction(string Name, string RawArgs, string[] Tokens) {
        if (CheckName(Name, "Function") == null)
            return null;

        string[] Args = SplitArgs(RawArgs);
        if (Args.Length == 0)
            return Error.CodeError("Syntax", "Function block span must be specified");

        object BlockSpan = Lexer.EvalExpr(Args[^1], Tokens, false, "number");
        if (BlockSpan == null) return null;
        if (!Keywords.CheckBlockSpan(BlockSpan, "Function")) return null;
    
        foreach(string Arg in Args.SkipLast(1).ToArray()) {
            int sep = Arg.IndexOf(':');
            if (sep == -1)
                return Error.CodeError("Syntax", "Invalid function argument declaration");
            string argname = Arg.Substring(0, sep).Trim();
            string argtype = Arg.Substring(sep + 1, Arg.Length-sep-1).Trim();
            if (CheckName(argname, "Function argument") == null) return null;
            if (!Keywords.DataTypes.Contains(argtype))
                return Error.CodeError("Type", $"{argtype} is an invalid function argument type");
        }
        return true;
    }

    protected internal static string[] FormPattern(string[] Tokens) {
        List<string> Pattern = new();
        for (int i = 0; i < Tokens.Length; i++) {
            if (Keywords.Functions.ContainsKey(Tokens[i]) && i == 0)
                Pattern.Add(Tokens[i] == "function" ? "function" : "keyword");
            else if (Keywords.DataTypes.Contains(Tokens[i]) && i == 0)
                Pattern.Add("variable");
            else if (Pattern.Count == 1 && Pattern?.ElementAt(0) == "variable")
                Pattern.Add("name");
            else if (Keywords.NameFunctions.Contains(Tokens[0]) && i == 1)
                Pattern.Add("name");
            else if (Keywords.NameFunctions.Contains(Tokens[0]) && i == 2)
                Pattern.Add("funcargs");
            else
                Pattern.Add("args");
        }
        return Pattern.ToArray();
    }


    private static bool CheckPattern(string[] Pattern) {
        return Patterns.Where(i =>
            Serialize(i) == Serialize(Pattern)
        ).Any();
    }

    private static object CheckName(string Name, string TokenType) {
        if (new Regex(@"[`!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?~]").Matches(Convert.ToString(Name[0])).Count > 0)
            return Error.CodeError("Syntax", $"{TokenType} name cannot contain special characters");
        if (new Regex("[0-9]").Matches(Convert.ToString(Name[0])).Count > 0)
            return Error.CodeError("Syntax", $"{TokenType} name cannot start with numeric character"); ;
        if (Name.Contains(' '))
            return Error.CodeError("Syntax", $"{TokenType} name cannot contain whitespace");
        return true;
    }

    private static object CheckVariable(string Type, string Name, string Value) {
        if (CheckName(Name, "Variable") == null)
            return null;

        if (Value == "__input__") return true;
        switch (Type) {
            case "string":
                return true;
                break;
            case "number":
                return CheckNumeric(Value) ? true : Error.CodeError("Type", $"Variable {Name} can only be assigned numeric values");
                break;
            case "bool":
                return new string[] { "True", "False" }.Contains(Value) ? true : Error.CodeError("Type", $"Variable {Name} can only be assigned boolean values");
            case "object":
                return CheckObject(Value);
        }
        return null;
    }

    private static object CheckObject(string PropString) {
        string[] properties = PropString.Split("<>");
        foreach (string prop in properties){
            int sep = prop.IndexOf(':');
            if (sep == -1) return Error.CodeError("Syntax", "Invalid object property declaration");
            string name = prop.Substring(0, sep).Trim();
            string type = prop.Substring(sep+1).Trim();
            if (CheckName(name, "Object property") == null) return null;
            if (!Keywords.DataTypes.Contains(type))
                return Error.CodeError("Type", $"{type} is an invalid object property type");
        }
        return true;
    }

    protected internal static object CheckArguments(string _Args, string[] ArgsList) {
        string[] RawArgs = SplitArgs(_Args);
        if (RawArgs.Length != ArgsList.Length)
            return Error.CodeError("Syntax", $"Expected {ArgsList.Length} arguments, received {RawArgs.Length}");

        // map data types to arguments
        string[] Args = RawArgs.Select(a => {
            string arg = ArgsList[Array.IndexOf(RawArgs, a)];
            if (arg.Contains("[]")) {
                var val = SetArrayElems(new string[] {arg, "nig", a});
                return val == null ? "string" : arg;
            }
            if (arg=="bool")
                return new object[] { "True", "False" }.Contains(a) ? "bool" : "string";
            if (arg=="number")
                return CheckNumeric(a) ? "number" : "string";
            else
                return "string";
        }).ToArray();

        for (int i = 0; i < Args.Length; i++) {
            if (RawArgs[i] == "")
                return Error.CodeError("Syntax", "No argument passed");
            if (Args[i] != (ArgsList[i]=="rawstring" ? "string": ArgsList[i]))
                return Error.CodeError("Type", $"Argument '{RawArgs[i]}' not of type {ArgsList[i]}");
        }
        return true;
    }

    protected internal static string[] SplitArgs(string args) {
        return args.Split("<>").Select(i => i.Trim()).ToArray();
    }

    protected internal static bool CheckNumeric(string value) {
        return (int.TryParse(value, out _) || (decimal.TryParse(value, out _)));
    }
}