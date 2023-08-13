using static Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript;
using System.Text.RegularExpressions;
using System.Data;
using static System.Text.Json.JsonSerializer;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using DickLang;

class Lexer : DickLang.Compiler {
    protected internal static object EvalExpr(string Expr, string[] Tokens, bool StrExpr, string LexType, bool CallError=true) {
        object FinalExpr = Expr.Replace("~[", "\uF480").Replace("]~", "\uF481");

        FinalExpr = ReplaceVariable(Convert.ToString(FinalExpr), StrExpr);
        if (FinalExpr==null) return null;
        
        object DExpr = ReplaceDestructure(Convert.ToString(FinalExpr), StrExpr);
        if (DExpr == null) return null;
        if (Convert.ToString(DExpr) != Convert.ToString(FinalExpr)) return ReplaceDebugChar(Convert.ToString(DExpr));

        if (
            Tokens.Length > 0 &&
            ((StrExpr && !Keywords.DataTypes.Contains(Tokens[0])) ||
            Keywords.Conditionals.Contains(Tokens[0]))
        ) FinalExpr = ReplaceString(Convert.ToString(FinalExpr), Keywords.Blocks.Contains(Tokens[0]));
        
        FinalExpr = Convert.ToString(FinalExpr)
                    .Replace("\uF480", "{")
                    .Replace("\uF481", "}")
                    .Replace("\uF482", ",");

        if (new string[] { "true", "false" }.Contains(Convert.ToString(FinalExpr).ToLower())) 
            FinalExpr = Serialize(Convert.ToBoolean(FinalExpr));

        string FinalType = new string[] { "true", "false" }.Contains(Convert.ToString(FinalExpr).ToLower()) ? "bool" :
            Parser.CheckNumeric(Convert.ToString(FinalExpr)) ? "number":"string";
        string ErrMsg = FinalType==LexType ? $"Failed {LexType} conversion" : $"Invalid conversion from {FinalType} to {LexType}";

        if (LexType == "object") {
            try {
                Deserialize<Dictionary<string, object>>(Convert.ToString(FinalExpr).Replace("\uF483", "\""));
                return Convert.ToString(FinalExpr).Replace("\uF483", "");
            } 
            catch {
                return Error.CodeError("Type", ErrMsg);
            }
        }
        FinalExpr = Convert.ToString(FinalExpr).Replace("\uF483", "");


        try {
            object result = EvaluateAsync(StrExpr ? $"$\"{FinalExpr}\"" : Convert.ToString(FinalExpr)).Result;
            if (LexType == "bool") {
                if (!new string[] { "true", "false" }.Contains(Convert.ToString(result).ToLower()))
                    return CallError ? Error.CodeError("Type", ErrMsg) : null;
                else
                    result = Convert.ToBoolean(result);
            }
            return result;
        }
        catch {
            if (LexType == "number" && FinalType == "string") {
                try {
                    DataTable table = new DataTable();
                    table.Compute(Convert.ToString(FinalExpr), "");
                    return CallError ? Error.CodeError("Type", ErrMsg) : null;
                } catch (OverflowException) {
                    return CallError ? Error.CodeError("Overflow", $"Numeric overflow {FinalExpr}") : null;
                } catch {
                    return CallError ? Error.CodeError("Type", ErrMsg) : null;
                }
            }
            return CallError ? Error.CodeError("Type", ErrMsg) : null;
        }
    }

    protected internal static object ReplaceVariable(string Expr, bool StrExpr) {
        List<KeyValuePair<int, string>> VarRef = new();

        for (int s = 0; s < Expr.Length; s++) {
            if (Expr[s] == '$') VarRef.Add(new KeyValuePair<int, string>(s, "Variable"));
            if (Expr[s] == '%') {
                if (FunctionInfo["Name"] == null)
                    return Error.RunTimeError("Syntax", "Cannot reference function arguments outside function block");
                VarRef.Add(new KeyValuePair<int, string>(s, "FuncArg"));
            }
        }

        List<int[]> Indexes = new();
        List<string> NewStrings = new();

        foreach (var pair in VarRef) {
            if (FunctionInfo["Name"] == null && pair.Value == "FuncArg") continue;
            string type = pair.Value == "Variable" ? "Variable" : "Function argument";
            var Coll = Deserialize<Dictionary<string, object>>(Serialize(
                (pair.Value == "Variable" ? Keywords.Variables : Keywords.Methods[FunctionInfo["Name"] as string]["Arguments"])
            ));

            if (StrExpr && !WithinInterpolate(Expr, pair.Key)) continue;
            if (WithinIndex(Expr, pair.Key)) continue;
            string name = GetANString(Expr, pair.Key + 1, 1).Trim();
            string arrname = null, objname = null;
            object VarValue;

            if (HasIndex(name) && name.Reverse().ToArray()[0]==']')
                arrname = name.Substring(0, name.IndexOf("["));

            if (HasProperty(name))
                objname = name.Substring(0, name.IndexOf("::"));
            
            if (!Coll.Keys.Contains(arrname ?? objname ?? name))
                return Error.CodeError("Reference", $"{type} {arrname ?? objname ?? name} does not exist");

            Indexes.Add(new int[] { pair.Key, pair.Key + name.Length });

            var Variable = Deserialize<Dictionary<string, object>>(Serialize(Coll[arrname ?? objname ?? name]));
            if (arrname!=null) {
                if (Variable["ArrayType"] == null)
                    return Error.RunTimeError("Reference", $"Cannot apply indexing to {arrname} of non-array type");
                VarValue = GetArrayElement(Variable["Value"], name, Convert.ToString(Variable["ArrayType"]));
                if (VarValue == null) return null;
            }
            else if (objname != null) {
                if (Variable["Properties"] == null)
                    return Error.RunTimeError("Reference", $"Cannot reference property of non-object type {objname}");
                VarValue = GetProperty(Variable["Properties"], name, objname);
                if (VarValue == null) return null;
            }
            else {
                VarValue = Convert.ToString(Variable["Value"]);
                if (Variable["ArrayType"] != null) {
                    var arr = Deserialize<object[]>(Serialize(Variable["Value"]));
                    VarValue = GetArrayString(arr);
                }
                else if (Variable["Properties"] != null)
                    VarValue = GetObjectString(Serialize(Variable["Properties"]));
            }
            NewStrings.Add(Convert.ToString(VarValue));
        }

        return ReplaceSubString(Expr, Indexes.ToArray(), NewStrings.ToArray());
    }

        private static object ReplaceDestructure(string Expr, bool StrExpr) {
        List<int> DRef = new();
        for (int s = 0; s < Expr.Length; s++) {
            if (s > Expr.Length - 3) break;
            if (Expr.Substring(s, 3) == "...")
                DRef.Add(s);
        }
        List<int[]> Indexes = new();
        List<string> NewStrings = new();
        
        foreach (var index in DRef) {
            if (StrExpr && !WithinInterpolate(Expr, index)) continue;
            if (WithinIndex(Expr, index) || (WithinQuotes(Expr, index))) continue;
            string value = GetANString(Expr, index+3, 1, true).Trim();

            object[] array; string DString;
            try {
                string arrstr = Regex.Unescape(Serialize(value));
                if (arrstr[0] == '"' && arrstr[arrstr.Length - 1] == '"') 
                    arrstr = arrstr.Substring(1, arrstr.Length - 2);
                arrstr = arrstr.Replace("\uF482", "\",\"");
                if (arrstr[0]=='[' && arrstr[arrstr.Length-1]==']')
                    arrstr = "[\"" + arrstr.Substring(1, arrstr.Length - 2) + "\"]";

                array = Deserialize<object[]>(arrstr);
                DString = String.Join(">>", array);
                Indexes.Add(new int[]{index, value.Length+3});
                NewStrings.Add(DString);
            } 
            catch {
                return Error.CodeError("Type", $"Cannot destructure non-container type {value}");
            }
        }
        return ReplaceSubString(Expr, Indexes.ToArray(), NewStrings.ToArray());
    }

    private static object ReplaceString(string Str, bool IsCondition) {
        string SubStr = "";
        List<int[]> Indexes = new(); List<string> NewStrings = new();

        bool CheckInterpolate(char c) => c == '\uF480' || c == '\uF481';
        bool FirstInterpolate(int i) {
            int index = GetInterpolate(Str, i, -1);
            return index == -1 ? false : Str[index] == '\uF480';
        };
        void AddSubstring(int[] i) {
            string s = Str.Substring(i[0], i[1]);
            if (Parser.CheckNumeric(s)) return;
            Indexes.Add(new int[] { i[0], i[0] + i[1] - 1 });
            NewStrings.Add(FirstInterpolate(i[0]) || IsCondition ? $"\"{s}\"" : s);
        };

        for (int i = 0; i < Str.Length; i++) {
            if (CheckSymbol(Convert.ToString(Str[i]))) {
                if (SubStr.Trim().Length > 0 && !SubStr.EndsWith('"'))
                    AddSubstring(new int[] { i - SubStr.Length, SubStr.Length });
                SubStr = "";
                continue;
            }
            if (!CheckInterpolate(Str[i])) SubStr += Str[i];
            if (i == Str.Length - 1 && !SubStr.EndsWith('"'))
                AddSubstring(new int[] { i - SubStr.Length + 1, SubStr.Length });
        }

        return ReplaceSubString(Str, Indexes.ToArray(), NewStrings.ToArray());
    }

    private static string ReplaceSubString(string Str, int[][] Indexes, string[] NewStrings) {
        string SavedStr = Str, NewStr = Str;
        for (int s = 0; s < NewStrings.Length; s++) {
            Indexes = Indexes.Select(i => new int[] { i[0] + NewStr.Length - SavedStr.Length, i[1] + NewStr.Length - SavedStr.Length }).ToArray();
            SavedStr = NewStr;

            string LString = NewStr.Substring(0, Indexes[s][0]);
            string RString = (NewStr.Length - Indexes[s][1] - 1) > 0 ? NewStr.Substring(Indexes[s][1] + 1, NewStr.Length - Indexes[s][1] - 1) : "";
            NewStr = $"{LString}{NewStrings[s]}{RString}";
        }
        return NewStr;
    }

    private static string GetANString(string str, int start, int inc, bool raw=false) {
        string res = "";
        int IndexCount = 0;
        for (int i = start; (inc > 0 ? i < str.Length : i >= 0); i += inc) {
            if (str[i] == '[' || str[i] == ']' && !raw) IndexCount++;
            if (IndexCount % 2 != 0 && !raw) {
                res += str[i];
                continue;
            }
            if (CheckSymbol(Convert.ToString(str[i]))) return res;
            res += str[i];
        }
        return res;
    }
    private static bool WithinInterpolate(string Expr, int Index) {
        int Start = GetInterpolate(Expr, Index - 1, -1);
        int End = GetInterpolate(Expr, Index + 2, 1);

        return (Start != -1 && End != -1);
    }

    private static bool WithinIndex(string Expr, int Index) {
        int GetInterpolate(int Inc) {
            for (int i=Index; (Inc > 0 ? i < Expr.Length : i >= 0); i += Inc) {
                if (Expr[i] == '[' || Expr[i] == ']') return i;
            }
            return -1;
        };
        int FirstIndex = GetInterpolate(-1);
        int LastIndex = GetInterpolate(1);
        if (new int[] {FirstIndex, LastIndex}.Any(i=>i==-1)) return false;
        if (Expr[FirstIndex] != '[' && Expr[LastIndex] != ']') return false;
        return true;
    }

    private static int GetInterpolate(string Str, int Start, int Inc) {
        for (int i = Start; (Inc > 0 ? i < Str.Length : i >= 0); i += Inc) {
            if (new Regex("[\uF480\uF481]").Matches(Convert.ToString(Str[i])).Count > 0)
                return i;
        }
        return -1;
    }
    
    private static bool WithinQuotes(string Str, int Index) {
        int GetQuote(int Start, int Inc) {
            for (int i = Start; (Inc > 0 ? i < Str.Length : i >= 0); i += Inc) {
                if (Str[i]=='"')
                    return i;
            }
            return -1;
        }
        int fQuote = GetQuote(Index, -1);
        int lQuote = GetQuote(Index, 1);
        return (fQuote != -1 && lQuote != -1);
    }

    protected internal static bool HasIndex(string Str) {
        List<int> i = new();
        foreach(char c in Str) {
            if (c == '[') i.Add(Str.IndexOf(c));
            if (c == ']') i.Add(Str.IndexOf(c));
        }
        if (i.Count() < 2) return false;
        if (i.ElementAt(0) > i.ElementAt(1)) return false;
        return true;
    }

    protected internal static bool HasProperty(string Str) {
        for (int i = 0; i < Str.Length; i++) {
            if (i == Str.Length - 1) break;
            if (Str[i] == ':' && Str[i + 1] == ':')
                return true;
        }
        return false;
    }

    private static string GetObjectString(string rawstring) {
        char iden = '\uF483';
        string GetPropString(string str) {
            if (str[0] == '\"' && str[^1] == '\"')
                str = str.Substring(1, str.Length-2);
            return str.Replace("\"", "\\\"");
        }

        List<string> ObjArr = new();
        var PropDic = Deserialize<Dictionary<string, Dictionary<string, object>>>(rawstring);
        foreach (string name in PropDic.Keys)
            ObjArr.Add($"{iden+name+iden}:{iden+GetPropString(Serialize(PropDic[name]["Value"]))+iden}");
        return $"{{{String.Join(",", ObjArr)}}}";
    }

    private static string GetArrayString(object[] val) {
        object[] rawelems = (object[])val;
        string elemstr = "";
        foreach (var elem in rawelems) {
            elemstr += Convert.ToString(elem).Replace("\"", "\\\"");
            if (Array.IndexOf(rawelems, elem) != rawelems.Length - 1) elemstr += "\uF482";
        }
        return $"[{elemstr}]";
    }

    private static object GetArrayElement(object RawArray, string VarName, string ArrayType) {
        string ArrName = VarName.Substring(0, VarName.IndexOf("["));
        object i = VarName.Substring(VarName.IndexOf("[") + 1, VarName.IndexOf("]")-VarName.IndexOf("[")-1);

        object Index = Lexer.EvalExpr(Convert.ToString(i), Array.Empty<string>(), false, "number", false);
        if (Index == null)
            return Error.RunTimeError("Reference", $"Index must be an integer");

        object[] TrueArray = Deserialize<object[]>(Serialize(RawArray));
        if (Convert.ToInt64(Index) < 0 || Convert.ToInt64(Index) >= TrueArray.Length)
            return Error.RunTimeError("Reference", $"Index {Index} is out of bounds");
    
        string val = Convert.ToString(TrueArray[Convert.ToInt64(Index)]);
        return ArrayType == "string" ? $"\"{val}\"" : val;
    }

    private static object GetProperty(object RawDic, string VarName, string ObjName) {
        var PropDic = Deserialize<Dictionary<string, Dictionary<string, object>>>(Serialize(RawDic));
        string propname = VarName.Substring(VarName.IndexOf("::") + 2);
        if (!PropDic.Keys.Contains(propname))
            return Error.RunTimeError("Reference", $"Object {ObjName} does not contain property {propname}");
        string val = Convert.ToString(PropDic[propname]["Value"]);
        return PropDic[propname]["Type"] == "string" ? $"\"{val}\"" : val;
    }

    private static string ReplaceDebugChar(string str) {
        return str.Replace("\uF480", "").Replace("\uF481", "");
    }

    private static bool CheckSymbol(string s) {
        return Keywords.Symbols.Matches(s).Count > 0 && s != "," && s != ".";
    }
}