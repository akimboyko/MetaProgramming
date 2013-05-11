using ApprovalTests.Core;
using ApprovalTests.Namers;

namespace MetaProgramming.RoslynCTP.Tests.Strategy
{
    public class StrategyNamer : IApprovalNamer
    {
        private readonly IApprovalNamer _namer = new UnitTestFrameworkNamer();
        private readonly string _strategyType;

        public StrategyNamer(string strategyType)
        {
            _strategyType = strategyType;
        }

        public string SourcePath { get { return _namer.SourcePath;  } }

        public string Name
        {
            get { return string.Concat(_namer.Name, ".", _strategyType); }
        }
    }
}