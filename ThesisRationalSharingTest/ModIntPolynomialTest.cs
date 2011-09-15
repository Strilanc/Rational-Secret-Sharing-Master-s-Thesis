using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ModIntPolynomialTest {
        [TestMethod()]
        public void ModIntPolynomialConstructorTest() {
            Assert.IsTrue(new ModIntPolynomial(new BigInteger[] { }, 7).ToString() == "0 (mod 7)");
            Assert.IsTrue(new ModIntPolynomial(new BigInteger[] { 1 }, 7).ToString() == "1 (mod 7)");
            Assert.IsTrue(new ModIntPolynomial(new BigInteger[] { 2, 3 }, 7).ToString() == "3x + 2 (mod 7)");
            Assert.IsTrue(new ModIntPolynomial(new BigInteger[] { 4, 5, 6 }, 7).ToString() == "6x^2 + 5x + 4 (mod 7)");
            ModIntTest.ExpectException(() => new ModIntPolynomial(new BigInteger[] { 4, 5, 6 }, 5));

            Assert.IsTrue(ModIntPolynomial.From(new int[0], 7).ToString() == "0 (mod 7)");
            Assert.IsTrue(ModIntPolynomial.From(new[] { 1 }, 7).ToString() == "1 (mod 7)");
            Assert.IsTrue(ModIntPolynomial.From(new[] { 2, 3 }, 7).ToString() == "3x + 2 (mod 7)");
            Assert.IsTrue(ModIntPolynomial.From(new[] { 4, 5, 6 }, 7).ToString() == "6x^2 + 5x + 4 (mod 7)");
            Assert.IsTrue(ModIntPolynomial.From(new[] { 4, 5, 6 }, 5).ToString() == "x^2 + 4 (mod 5)");
        }

        [TestMethod()]
        public void DivRemTest() {
            ModIntPolynomial f1 = ModIntPolynomial.From(new[] { 1, 1 }, 5);
            ModIntPolynomial f2 = ModIntPolynomial.From(new[] { 3, 2 }, 5);
            ModIntPolynomial p = f1 * f2;

            Assert.IsTrue(p.DivRem(f1).Item1 == f2);
            Assert.IsTrue(p.DivRem(f1).Item2 == ModIntPolynomial.From(new int[0], 5));
            Assert.IsTrue(p.DivRem(f2).Item1 == f1);
            Assert.IsTrue(p.DivRem(f2).Item2 == ModIntPolynomial.From(new int[0], 5));
            
            Assert.IsTrue(f1.DivRem(p).Item1 == ModIntPolynomial.From(new int[0], 5));
            Assert.IsTrue(f1.DivRem(p).Item2 == f1);
            Assert.IsTrue(f2.DivRem(p).Item1 == ModIntPolynomial.From(new int[0], 5));
            Assert.IsTrue(f2.DivRem(p).Item2 == f2);

            Assert.IsTrue(f2.DivRem(f1).Item1 == ModIntPolynomial.From(new[] { 2 }, 5));
            Assert.IsTrue(f2.DivRem(f1).Item2 == ModIntPolynomial.From(new[] { 1 }, 5));
        }

        /// <summary>
        ///A test for Equals
        ///</summary>
        [TestMethod()]
        public void EqualsTest() {
            ModIntPolynomial f1 = ModIntPolynomial.From(new[] { 1, 1 }, 5);
            ModIntPolynomial f2 = ModIntPolynomial.From(new[] { 2, 3 }, 5);
            ModIntPolynomial g1 = ModIntPolynomial.From(new[] { 1, 1 }, 5);
            ModIntPolynomial g2 = ModIntPolynomial.From(new[] { 2, 3 }, 5);

            Assert.IsTrue(f1 == g1);
            Assert.IsTrue(g1 == f1);
            Assert.IsTrue(f2 == g2);
            Assert.IsTrue(g2 == f2);
            Assert.IsTrue(f1 != g2);
            Assert.IsTrue(g1 != f2);
            Assert.IsTrue(f2 != g1);
            Assert.IsTrue(g2 != f1);
            
            Assert.IsFalse(f1 != g1);
            Assert.IsFalse(g1 != f1);
            Assert.IsFalse(f2 != g2);
            Assert.IsFalse(g2 != f2);
            Assert.IsFalse(f1 == g2);
            Assert.IsFalse(g1 == f2);
            Assert.IsFalse(f2 == g1);
            Assert.IsFalse(g2 == f1);

            Assert.IsTrue(f1.Equals(g1));
            Assert.IsTrue(g1.Equals(f1));
            Assert.IsTrue(f2.Equals(g2));
            Assert.IsTrue(g2.Equals(f2));
            Assert.IsFalse(f1.Equals(g2));
            Assert.IsFalse(g1.Equals(f2));
            Assert.IsFalse(f2.Equals(g1));
            Assert.IsFalse(g2.Equals(f1));
        }

        [TestMethod()]
        public void EvaluateAtTest() {
            var zero = ModIntPolynomial.From(new int[0], 11);
            var one = ModIntPolynomial.From(new int[] { 1 }, 11);
            var X = one << 1;
            var XSquaredPlusOne = X * X + one;

            Assert.IsTrue(zero.EvaluateAt(1) == 0);
            Assert.IsTrue(zero.EvaluateAt(3) == 0);
            Assert.IsTrue(one.EvaluateAt(3) == 1);
            Assert.IsTrue(one.EvaluateAt(4) == 1);
            Assert.IsTrue(one.EvaluateAt(11) == 1);
            Assert.IsTrue(X.EvaluateAt(0) == 0);
            Assert.IsTrue(X.EvaluateAt(4) == 4);
            Assert.IsTrue(X.EvaluateAt(7) == 7);
            Assert.IsTrue(X.EvaluateAt(11) == 0);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(1) == 2);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(2) == 5);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(3) == 10);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(4) == 6);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(5) == 4);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(12) == 2);
        }

        [TestMethod()]
        public void FromInterpolationTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var s1 = Tuple.Create(b + 1, b + 2);
            var s2 = Tuple.Create(b + 2, b + 3);
            var s3 = Tuple.Create(b + 3, b + 5);
            var s4 = Tuple.Create(b + 4, b + 7);
            var s5 = Tuple.Create(b + 5, ModIntPolynomial.FromInterpolation(new[] { s1, s2, s3, s4 }, m).EvaluateAt(5));
            var s6 = Tuple.Create(b + 6, ModIntPolynomial.FromInterpolation(new[] { s1, s2, s3, s4 }, m).EvaluateAt(6));

            // constant
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s1 }, m).EvaluateAt(0) == s1.Item2);
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s1 }, m).EvaluateAt(95) == s1.Item2);
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s2 }, m).EvaluateAt(0) == s2.Item2);
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s2 }, m).EvaluateAt(-1) == s2.Item2);

            // line
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s1, s2 }, m).EvaluateAt(0) == 1);
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s1, s2 }, m).EvaluateAt(-1) == 0);
            Assert.IsTrue(ModIntPolynomial.FromInterpolation(new[] { s1, s2 }, m).EvaluateAt(6) == 7);
        }

        [TestMethod()]
        public void ArithmeticTest() {
            var one = ModIntPolynomial.From(new int[] { 1 }, 11);
            var X = one << 1;
            Assert.IsTrue((X + one) * (X + one) * (X + one) == (X + one) * (X * X + 2 * X + one));
            Assert.IsTrue(X << 1 == X * X);
            Assert.IsTrue(X << 2 == X * X * X);
            Assert.IsTrue((X + one) << 3 == (X + one) * X * X * X);
            Assert.IsTrue((X + one) * (X + one) / (X + one) == (X + one));
            Assert.IsTrue((-X - one) == -(X + one));
        }
    }
}
