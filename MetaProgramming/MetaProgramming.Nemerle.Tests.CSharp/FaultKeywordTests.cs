using System;
using FluentAssertions;
using NUnit.Framework;

namespace MetaProgramming.Nemerle.Tests.CSharp
{
    [TestFixture]
    public class FaultKeywordTests
    {
        [Test]
        public void FaultKeywordAtNemerle_NormalFlow_BodyHasBeenExecuted()
        {
            var finallyHasBeenExecuted = false;
            var uniqueString = Guid.NewGuid().ToString();

            new FaultKeywordSample()
                    .ExecuteFaultCSharpNemerle(
                        () => uniqueString,
                        () => { throw new Exception("Should not execute `fault`"); },
                        () => { finallyHasBeenExecuted = true; })
                    .Should().Be(uniqueString);

            finallyHasBeenExecuted.Should().BeTrue();
        }

        [Test]
        public void FaultKeywordAtNemerle_ExceptionalFlow_BothBodyAndFaultHadBeenExecuted()
        {
            var bodyHasBeenExecuted = false;
            var faultHasBeenExecuted = false;
            var finallyHasBeenExecuted = false;
            var uniqueExceptionalMessage = string.Format("Exceptional Flow {0}", Guid.NewGuid());

            Action act = () =>
                new FaultKeywordSample()
                    .ExecuteFaultCSharpNemerle(
                        () =>
                        {
                            bodyHasBeenExecuted = true;
                            throw new Exception(uniqueExceptionalMessage);
                        },
                        () => { faultHasBeenExecuted = true; },
                        () => { finallyHasBeenExecuted = true; });

            act.ShouldThrow<Exception>()
                .WithMessage(uniqueExceptionalMessage);

            bodyHasBeenExecuted.Should().BeTrue();
            faultHasBeenExecuted.Should().BeTrue();
            finallyHasBeenExecuted.Should().BeTrue();
        }
    }
}
