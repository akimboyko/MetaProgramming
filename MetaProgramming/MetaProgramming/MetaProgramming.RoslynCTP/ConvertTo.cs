namespace MetaProgramming.RoslynCTP
{
    public static class ConvertTo
    {
        public static string VisualBasic(string sourceCode)
        {
            return new CSharpToVisualBasicConverter.Converting.Converter().Convert(sourceCode);
        }

        public static string CSharp(string sourceCode)
        {
            var tree = Roslyn.Compilers.VisualBasic.SyntaxTree.ParseText(sourceCode);
            return new VisualBasicToCSharpConverter.Converting.Converter().Convert(tree).ToFullString();
        }
    }
}
