using System;
using System.Diagnostics;
using System.IO;
using Coop.Library.Common;

namespace Coop.Library.Artefacts.Tests
{
    [TestFixture]
    public class ArtefactMoveRetryTests
    {
        [Test]
        public void Test_That_We_Can_RetryAnAccessDeniedMoveTwiceAndSucceedOnTheThirdAttempt()
        {
            int attempts = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            AtomicFile.RetryOnAccessDenied(() =>
            {
                attempts = attempts + 1;
                if (attempts < 3)
                {
                    throw Denied();
                }
            });

            stopwatch.Stop();
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(2 * AtomicFile.RetryDelayMilliseconds));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000));
        }

        [Test]
        public void Test_That_We_Can_GiveUpAfterTheAttemptLimitWhenEveryMoveIsDenied()
        {
            int attempts = 0;

            Assert.Throws<IOException>(() => AtomicFile.RetryOnAccessDenied(() =>
            {
                attempts = attempts + 1;
                throw Denied();
            }));

            Assert.That(attempts, Is.EqualTo(AtomicFile.AttemptLimit));
        }

        [Test]
        public void Test_That_We_Can_RethrowANonAccessDeniedFailureWithoutRetrying()
        {
            int attempts = 0;

            Assert.Throws<IOException>(() => AtomicFile.RetryOnAccessDenied(() =>
            {
                attempts = attempts + 1;
                throw new IOException("Cannot create a file when that file already exists");
            }));

            Assert.That(attempts, Is.EqualTo(1));
        }

        private static IOException Denied()
        {
            return new IOException("Access to the path is denied", unchecked((int)0x80070005));
        }
    }
}
