namespace MetaProgramming.RoslynCTP.Model
{
    public class Complexity
    {
        public string TypeIdentifier { get; set; }
        public string MethodIdentifier { get; set; }
        public string SourcesSample { get; set; }
        public int nStatementSyntax { get; set; }
        public string FilePath { get; set; }
        public int SourceLine { get; set; }
    }
}
