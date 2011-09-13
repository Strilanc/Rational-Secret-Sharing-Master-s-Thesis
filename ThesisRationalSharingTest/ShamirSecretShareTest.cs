using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ShamirSecretShareTest {
        [TestMethod()]
        public void ShamirSecretShareConstructorTest() {
            var s = new ShamirSecretShare(new ModInt(0, 2), new ModInt(1, 2));
            Assert.IsTrue(s.X == 0);
            Assert.IsTrue(s.Y == new ModInt(1, 2));
        }

        [TestMethod()]
        public void InterpolateSimpleTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var s1 = new ShamirSecretShare(b + 1, b + 2);
            var s2 = new ShamirSecretShare(b + 2, b + 3);
            var s3 = new ShamirSecretShare(b + 3, b + 5);
            var s4 = new ShamirSecretShare(b + 4, b + 7);
            var s5 = ShamirSecretShare.Interpolate(new[] { s1, s2, s3, s4 }, b + 5);
            var s6 = ShamirSecretShare.Interpolate(new[] { s1, s2, s3, s4 }, b + 6);

            // constant
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s1 }, b + 0).Y == s1.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s1 }, b + 95).Y == s1.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s2 }, b + 0).Y == s2.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s2 }, b + -1).Y == s2.Y);

            // line
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s1, s2 }, b + 0).Y == 1);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s1, s2 }, b + -1).Y == 0);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s1, s2 }, b + 6).Y == 7);
        }

        [TestMethod()]
        public void InterpolateConsistencyTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var s1 = new ShamirSecretShare(b + 1, b + 2);
            var s2 = new ShamirSecretShare(b + 2, b + 3);
            var s3 = new ShamirSecretShare(b + 3, b + 5);
            var s4 = new ShamirSecretShare(b + 4, b + 7);
            var s5 = ShamirSecretShare.Interpolate(new[] { s1, s2, s3, s4 }, b + 5);
            var s6 = ShamirSecretShare.Interpolate(new[] { s1, s2, s3, s4 }, b + 6);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s2, s3, s4, s5 }, b + 1).Y == s1.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s2, s3, s4, s6 }, b + 1).Y == s1.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s6, s3, s4, s5 }, b + 2).Y == s2.Y);
            Assert.IsTrue(ShamirSecretShare.Interpolate(new[] { s6, s2, s4, s5 }, b + 3).Y == s3.Y);
        }

        [TestMethod()]
        public void InterpolateRangeTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var s1 = new ShamirSecretShare(b + 1, b + 2);
            var s2 = new ShamirSecretShare(b + 2, b + 3);
            var s3 = new ShamirSecretShare(b + 3, b + 5);
            var s4 = new ShamirSecretShare(b + 4, b + 7);
            var s5s = Enumerable.Range(0, m).Select(i => new ShamirSecretShare(b + 9, b + i));
            var y9s = s5s.Select(e => ShamirSecretShare.Interpolate(new[] { s1, s2, s3, s4, e }, b + 11).Y.Value);
            Assert.IsTrue(y9s.Distinct().Count() == m);
        }
    }
}
