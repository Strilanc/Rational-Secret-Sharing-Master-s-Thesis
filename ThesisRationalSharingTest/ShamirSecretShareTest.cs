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
            var s5 = ShamirSecretShare.FromPoly(ShamirSecretShare.InterpolatePoly(new[] { s1, s2, s3, s4 }), 5);
            var s6 = ShamirSecretShare.FromPoly(ShamirSecretShare.InterpolatePoly(new[] { s1, s2, s3, s4 }), 6);

            // constant
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s1 }).EvaluateAt(0) == s1.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s1 }).EvaluateAt(95) == s1.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s2 }).EvaluateAt(0) == s2.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s2 }).EvaluateAt(-1) == s2.Y);

            // line
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s1, s2 }).EvaluateAt(0) == 1);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s1, s2 }).EvaluateAt(-1) == 0);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s1, s2 }).EvaluateAt(6) == 7);
        }

        [TestMethod()]
        public void InterpolateConsistencyTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var s1 = new ShamirSecretShare(b + 1, b + 2);
            var s2 = new ShamirSecretShare(b + 2, b + 3);
            var s3 = new ShamirSecretShare(b + 3, b + 5);
            var s4 = new ShamirSecretShare(b + 4, b + 7);
            var s5 = new ShamirSecretShare(b + 5, b + ShamirSecretShare.InterpolatePoly(new[] { s1, s2, s3, s4 }).EvaluateAt(5));
            var s6 = new ShamirSecretShare(b + 6, b + ShamirSecretShare.InterpolatePoly(new[] { s1, s2, s3, s4 }).EvaluateAt(6));
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s2, s3, s4, s5 }).EvaluateAt(1) == s1.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s2, s3, s4, s6 }).EvaluateAt(1) == s1.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s6, s3, s4, s5 }).EvaluateAt(2) == s2.Y);
            Assert.IsTrue(ShamirSecretShare.InterpolatePoly(new[] { s6, s2, s4, s5 }).EvaluateAt(3) == s3.Y);
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
            var y9s = s5s.Select(e => ShamirSecretShare.InterpolatePoly(new[] { s1, s2, s3, s4, e }).EvaluateAt(11));
            Assert.IsTrue(y9s.Distinct().Count() == m);
        }
    }
}
