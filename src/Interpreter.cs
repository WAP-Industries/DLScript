using System.Runtime.InteropServices;
using static DickLang.Compiler;
using static System.Text.Json.JsonSerializer;

class Interpreter : DickLang.Compiler {
    private static object[] GetArgs(string[] Tokens, string ArgString) {
        string[] Args = Parser.SplitArgs(ArgString);
        string[] ArgsList = (Keywords.DataTypes.Contains(Tokens[0]) ? new string[] { Tokens[0] } : Keywords.Functions[Tokens[0]].Info.Args);

        bool GetStrExpr(string Arg){
            return Keywords.DataTypes.Contains(Tokens[0]) ? Tokens[0] == "string" : ArgsList[Array.IndexOf(Args, Arg)] == "string";
        };
        string GetLexType(string Arg){
            return Keywords.DataTypes.Contains(Tokens[0]) ? Tokens[0].Replace("[]", ""): ArgsList[Array.IndexOf(Args, Arg)];
        }
        return Args.Select(i => 
            ArgsList[Array.IndexOf(Args, i)]=="rawstring" ? i: Lexer.EvalExpr(i, Tokens, GetStrExpr(i), GetLexType(i))
        ).ToArray();
    }
    protected internal static object Interprete(string[] Tokens) {
        // call function
        if (Keywords.Functions.ContainsKey(Tokens[0]))
            return Keywords.Functions[Tokens[0]].Func(
                Parser.FormPattern(Tokens).Contains("args") ? GetArgs(Tokens, Tokens[1]) :
                (Tokens[0]=="function" ? new string[] { Tokens[1], Tokens[2] } : 
                (Keywords.NameFunctions.Contains(Tokens[0]) ? new string[] { Tokens[1], Tokens.Length==3 ? Tokens[2] :null } : Array.Empty<string>()))
            );

        // declare variable
        if (Keywords.DataTypes.Contains(Tokens[0])) {
            object val;
            if (Tokens[0].Contains("[]"))
                val = Parser.SetArrayElems(Tokens);
            else if (Tokens[0] == "object")
                val = Keywords.CreateObject(Tokens[2]);
            else
                val = Tokens[2] == "__input__" ? GetInput(Tokens[0]) : GetArgs(Tokens, Tokens[2])[0];
            
            if (val == null)
                return Error.RunTimeError("Type", "Input type and variable type do not match");

            if (Keywords.Variables.ContainsKey(Tokens[1]))
                Keywords.Variables.Remove(Tokens[1]);

            Keywords.Variables.Add(
                Tokens[1], new Dictionary<string, object>()
                {
                    { "Type", Tokens[0] },
                    { "ArrayType", Tokens[0].Contains("[]") ? Tokens[0].Replace("[]", "") : null },
                    { "Properties", Tokens[0]=="object" ? val:null},
                    { "Value", Tokens[0].Contains("[]") ? val : 
                                Tokens[0]=="object" ? $"\"{Keywords.DefaultValues["object"]}\"" : 
                                val},
                    { "Attributes", Keywords.SetAttribute(val, Tokens[0]) }
                }
            );
            return true;
        }
        return null;
    }

    private static object GetInput(string Type) {
        var val = Console.ReadLine();
        if (Type == "string") return Convert.ToString(val);
        if (Type == "number")
            return Parser.CheckNumeric(val) ? Convert.ToDouble(val) : null;
        if (Type == "bool")
            return new string[] { "True", "False" }.Contains(Convert.ToString(val)) ? Convert.ToBoolean(val) : null;

        return null;
    }
}