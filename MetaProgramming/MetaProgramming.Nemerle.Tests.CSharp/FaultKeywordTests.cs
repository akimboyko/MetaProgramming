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
            new FaultKeywordSample()
                    .ExecuteFaultCSharpNemerle(
                        () => "body",
                        () => { throw new Exception("Should not execute `fault`"); })
                    .Should().Be("body");
        }

        [Test]
        public void FaultKeywordAtNemerle_ExceptionalFlow_BothBodyAndFaultHadBeenExecuted()
        {
            var bodyHasBeenExecuted = false;
            var faultHasBeenExecuted = false;

            Action act = () =>
                new FaultKeywordSample()
                    .ExecuteFaultCSharpNemerle(
                        () =>
                        {
                            bodyHasBeenExecuted = true;
                            throw new Exception("Exceptional Flow");
                        },
                        () =>
                        {
                            faultHasBeenExecuted = true;
                        });

            act.ShouldThrow<Exception>()
                .WithMessage("Exceptional Flow");

            bodyHasBeenExecuted.Should().BeTrue();
            faultHasBeenExecuted.Should().BeTrue();
        }
    }
}
