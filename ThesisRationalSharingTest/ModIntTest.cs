using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ModIntTest {
        public static void ExpectException(Action action) {
            try {
                action.Invoke();
                Assert.Fail("Expected an exception");
            } catch (Exception) {
                // good
            }
        }
        
        [TestMethod()]
        public void ModIntConstructorTest() {
            Assert.IsTrue(new ModInt(2, 3).Value == 2);
            Assert.IsTrue(new ModInt(2, 3).Modulus == 3);
            Assert.IsTrue(new ModInt(0, 1).Value == 0);
            Assert.IsTrue(new ModInt(0, 1).Modulus == 1);
            ExpectException(() => new ModInt(0, -1));
            ExpectException(() => new ModInt(0, 0));
            ExpectException(() => new ModInt(-1, 1));
            ExpectException(() => new ModInt(102, 101));
            ExpectException(() => new ModInt(101, 101));
        }

        [TestMethod()]
        public void EqualsTest() {
            Assert.IsFalse(new ModInt(2, 3) == new ModInt(4, 5));
            Assert.IsFalse(new ModInt(2, 3) == new ModInt(2, 4));
            Assert.IsFalse(new ModInt(2, 3) == new ModInt(1, 3));
            Assert.IsTrue(new ModInt(2, 3) == new ModInt(2, 3));

            Assert.IsTrue(new ModInt(2, 3) == 2);
            Assert.IsTrue(new ModInt(2, 4) == 2);
            Assert.IsFalse(new ModInt(2, 3) != 2);
            Assert.IsFalse(new ModInt(2, 4) != 2);

            Assert.IsFalse(2 != new ModInt(2, 3));
            Assert.IsFalse(2 != new ModInt(2, 4));
            Assert.IsTrue(2 == new ModInt(2, 3));
            Assert.IsTrue(2 == new ModInt(2, 4));

            Assert.IsTrue(new ModInt(2, 3).Equals(new ModInt(2, 3)));
            Assert.IsTrue(!new ModInt(2, 3).Equals(new ModInt(1, 3)));
            Assert.IsTrue(!new ModInt(2, 3).Equals(new ModInt(2, 4)));
            Assert.IsTrue(!new ModInt(2, 3).Equals(new ModInt(3, 4)));
            Assert.IsTrue(!new ModInt(2, 3).Equals(2));
        }

        [TestMethod()]
        public void FromTest() {
            Assert.IsTrue(ModInt.From(11, 10) == 1);
            Assert.IsTrue(ModInt.From(10, 10) == 0);
            Assert.IsTrue(ModInt.From(9, 10) == 9);
            Assert.IsTrue(ModInt.From(1, 10) == 1);
            Assert.IsTrue(ModInt.From(0, 10) == 0);
            Assert.IsTrue(ModInt.From(-1, 10) == 9);
            Assert.IsTrue(ModInt.From(-9, 10) == 1);
            Assert.IsTrue(ModInt.From(-10, 10) == 0);
            Assert.IsTrue(ModInt.From(-11, 10) == 9);
        }

        [TestMethod()]
        public void op_AdditionTest() {
            Assert.IsTrue(new ModInt(0, 5) + 3 == 3);
            Assert.IsTrue(new ModInt(4, 5) + 3 == 2);
            Assert.IsTrue(new ModInt(4, 5) + -10 == 4);
        }
        [TestMethod()]
        public void op_MultiplyTest() {
            Assert.IsTrue(new ModInt(0, 5) * 3 == 0);
            Assert.IsTrue(new ModInt(4, 5) * 3 == 2);
            Assert.IsTrue(new ModInt(4, 5) * -9 == 4);
        }
        [TestMethod()]
        public void op_SubtractionTest() {
            Assert.IsTrue(new ModInt(0, 5) - 3 == 2);
            Assert.IsTrue(new ModInt(4, 5) - 3 == 1);
            Assert.IsTrue(new ModInt(4, 5) - -10 == 4);
        }
        [TestMethod()]
        public void op_UnaryNegationTest() {
            Assert.IsTrue(-new ModInt(0, 5) == 0);
            Assert.IsTrue(-new ModInt(4, 5) == 1);
            Assert.IsTrue(-new ModInt(3, 5) == 2);
        }

        [TestMethod()]
        public void MultiplicativeInverseTest() {
            Assert.IsTrue(new ModInt(1, 5).MultiplicativeInverse() == 1);
            Assert.IsTrue(new ModInt(2, 5).MultiplicativeInverse() == 3);
            Assert.IsTrue(new ModInt(3, 5).MultiplicativeInverse() == 2);
            Assert.IsTrue(new ModInt(4, 5).MultiplicativeInverse() == 4);
        }
    }
}
