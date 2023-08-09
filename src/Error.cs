using static System.Console;
using static DickLang.Compiler;

class Error : DickLang.Compiler {
    public static object CodeError(string Type, string Info) {
        WriteLine($"\n{Type}Error: {Info}\n\tat line {LineNumber + 1}: {CurrentCode[LineNumber].Trim()}");
        return null;
    }

    public static object RunTimeError(string Type, string Info) {
        CodeError(Type, Info);
        DickLang.Compiler.LineNumber = DickLang.Compiler.CurrentCode.Length;
        return null;
    }
}
