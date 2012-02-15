using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ModIntPolynomialTest {
        [TestMethod()]
        public void ModIntPolynomialConstructorTest() {
            var f7 = new ModIntField(7);
            var f5 = new ModIntField(5);
            var b = new ModInt(0, 7);
            var c = new ModInt(0, 5);
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f7, new ModInt[0]).ToString() == "0 (mod 7)");
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]).ToString() == "0 (mod 5)");
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f7, new[] { b+1 }).ToString() == "1 (mod 7)");
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f7, new[] { b + 2, b + 3 }).ToString() == "3x + 2 (mod 7)");
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f7, new[] { b + 4, b + 5, b + 6 }).ToString() == "6x^2 + 5x + 4 (mod 7)");
            Assert.IsTrue(Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 4, c + 5, c + 6 }).ToString() == "x^2 + 4 (mod 5)");
        }

        [TestMethod()]
        public void DivRemTest() {
            var f5 = new ModIntField(5);
            var c = new ModInt(0, 5);
            Polynomial<ModInt> f1 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 1, c + 1 });
            Polynomial<ModInt> f2 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 3, c + 2 });
            Polynomial<ModInt> p = f1 * f2;

            Assert.IsTrue(p.DivRem(f1).Item1 == f2);
            Assert.IsTrue(p.DivRem(f1).Item2 == Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]));
            Assert.IsTrue(p.DivRem(f2).Item1 == f1);
            Assert.IsTrue(p.DivRem(f2).Item2 == Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]));

            Assert.IsTrue(f1.DivRem(p).Item1 == Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]));
            Assert.IsTrue(f1.DivRem(p).Item2 == f1);
            Assert.IsTrue(f2.DivRem(p).Item1 == Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]));
            Assert.IsTrue(f2.DivRem(p).Item2 == f2);

            Assert.IsTrue(f2.DivRem(f1).Item1 == Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 2 }));
            Assert.IsTrue(f2.DivRem(f1).Item2 == Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 1 }));
        }

        /// <summary>
        ///A test for Equals
        ///</summary>
        [TestMethod()]
        public void EqualsTest() {
            var f5 = new ModIntField(5);
            var c = new ModInt(0, 5);
            Polynomial<ModInt> f1 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 1, c + 1 });
            Polynomial<ModInt> f2 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 2, c + 3 });
            Polynomial<ModInt> g1 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 1, c + 1 });
            Polynomial<ModInt> g2 = Polynomial<ModInt>.FromCoefficients(f5, new[] { c + 2, c + 3 });

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
            var f5 = new ModIntField(11);
            var b = new ModInt(0, 11);
            var zero = Polynomial<ModInt>.FromCoefficients(f5, new ModInt[0]);
            var one = Polynomial<ModInt>.FromCoefficients(f5, new ModInt[] { b+1 });
            var X = one << 1;
            var XSquaredPlusOne = X * X + one;

            Assert.IsTrue(zero.EvaluateAt(b+1) == 0);
            Assert.IsTrue(zero.EvaluateAt(b+3) == 0);
            Assert.IsTrue(one.EvaluateAt(b+3) == 1);
            Assert.IsTrue(one.EvaluateAt(b+4) == 1);
            Assert.IsTrue(one.EvaluateAt(b+11) == 1);
            Assert.IsTrue(X.EvaluateAt(b+0) == 0);
            Assert.IsTrue(X.EvaluateAt(b+4) == 4);
            Assert.IsTrue(X.EvaluateAt(b+7) == 7);
            Assert.IsTrue(X.EvaluateAt(b+11) == 0);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+1) == 2);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+2) == 5);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+3) == 10);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+4) == 6);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+5) == 4);
            Assert.IsTrue(XSquaredPlusOne.EvaluateAt(b+12) == 2);
        }

        [TestMethod()]
        public void FromInterpolationTest() {
            var m = 103;
            var b = new ModInt(0, m);
            var fm = new ModIntField(m);
            var s1 = new Point<ModInt>(fm, b + 1, b + 2);
            var s2 = new Point<ModInt>(fm, b + 2, b + 3);
            var s3 = new Point<ModInt>(fm, b + 3, b + 5);
            var s4 = new Point<ModInt>(fm, b + 4, b + 7);
            var s5 = new Point<ModInt>(fm, b + 5, Polynomial<ModInt>.FromInterpolation(fm, new[] { s1, s2, s3, s4 }).EvaluateAt(b + 5));
            var s6 = new Point<ModInt>(fm, b + 6, Polynomial<ModInt>.FromInterpolation(fm, new[] { s1, s2, s3, s4 }).EvaluateAt(b + 6));

            // constant
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s1 }).EvaluateAt(b + 0) == s1.Y);
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s1 }).EvaluateAt(b + 95) == s1.Y);
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s2 }).EvaluateAt(b + 0) == s2.Y);
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s2 }).EvaluateAt(b + -1) == s2.Y);

            // line
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s1, s2 }).EvaluateAt(b + 0) == 1);
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s1, s2 }).EvaluateAt(b + -1) == 0);
            Assert.IsTrue(Polynomial<ModInt>.FromInterpolation(fm, new[] { s1, s2 }).EvaluateAt(b + 6) == 7);
        }

        [TestMethod()]
        public void ArithmeticTest() {
            var one = Polynomial<ModInt>.FromCoefficients(new ModIntField(11), new[] { new ModInt(1, 11) });
            var X = one << 1;
            Assert.IsTrue((X + one) * (X + one) * (X + one) == (X + one) * (X * X + new ModInt(2, 11) * X + one));
            Assert.IsTrue(X << 1 == X * X);
            Assert.IsTrue(X << 2 == X * X * X);
            Assert.IsTrue((X + one) << 3 == (X + one) * X * X * X);
            Assert.IsTrue((X + one) * (X + one) / (X + one) == (X + one));
            Assert.IsTrue((-X - one) == -(X + one));
        }
    }
}
