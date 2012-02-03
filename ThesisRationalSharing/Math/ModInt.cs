using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Modular integers with associated arithmetic.</summary>
[DebuggerDisplay("{ToString()}")]
public struct ModInt : IEquatable<ModInt>, IFiniteField<ModInt> {
    public readonly BigInteger Modulus;
    public readonly BigInteger Value;

    /** Creates a modular integer from the given reduced value and modulus. */
    public ModInt(BigInteger value, BigInteger modulus) {
        Contract.Requires(modulus > 0);
        Contract.Requires(value >= 0);
        Contract.Requires(value < modulus);
        this.Modulus = modulus;
        this.Value = value;
    }

    /** Creates a modular integer from the given value and modulus, reducing the value if necessary. */
    public static ModInt From(BigInteger value, BigInteger modulus) {
        Contract.Requires(modulus > 0);
        return new ModInt(value.ProperMod(modulus), modulus);
    }

    public static ModInt operator -(ModInt value) {
        return From(-value.Value, value.Modulus);
    }
    public static ModInt operator +(ModInt value1, ModInt value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        return From(value1.Value + value2.Value, value1.Modulus);
    }
    public static ModInt operator -(ModInt value1, ModInt value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        return From(value1.Value - value2.Value, value1.Modulus);
    }
    public static ModInt operator *(ModInt value1, ModInt value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        return From(value1.Value * value2.Value, value1.Modulus);
    }

    public static ModInt operator +(ModInt value1, BigInteger value2) {
        return From(value1.Value + value2, value1.Modulus);
    }
    public static ModInt operator -(ModInt value1, BigInteger value2) {
        return From(value1.Value - value2, value1.Modulus);
    }
    public static ModInt operator *(ModInt value1, BigInteger value2) {
        return From(value1.Value * value2, value1.Modulus);
    }
    public static ModInt operator +(BigInteger value1, ModInt value2) {
        return From(value1 + value2.Value, value2.Modulus);
    }
    public static ModInt operator -(BigInteger value1, ModInt value2) {
        return From(value1 - value2.Value, value2.Modulus);
    }
    public static ModInt operator *(BigInteger value1, ModInt value2) {
        return From(value1 * value2.Value, value2.Modulus);
    }

    public static bool operator ==(BigInteger value1, ModInt value2) {
        return value1 == value2.Value;
    }
    public static bool operator !=(BigInteger value1, ModInt value2) {
        return value1 != value2.Value;
    }
    public static bool operator ==(ModInt value1, ModInt value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(ModInt value1, ModInt value2) {
        return !value1.Equals(value2);
    }
    public static bool operator ==(ModInt value1, BigInteger value2) {
        return value1.Value == value2;
    }
    public static bool operator !=(ModInt value1, BigInteger value2) {
        return value1.Value != value2;
    }

    private static Tuple<BigInteger, BigInteger> ExtendedGCD(BigInteger a, BigInteger b) {
        if (b == 0) return Tuple.Create(BigInteger.One, BigInteger.Zero);
        var q = a / b;
        var r = a % b;
        var st  = ExtendedGCD(b, r);
        var s = st.Item1;
        var t = st.Item2;
        return Tuple.Create(t, s - q * t);
    }
    public ModInt MultiplicativeInverse {
        get {
            return From(ExtendedGCD(Value, Modulus).Item1, Modulus);
        }
    }

    public bool Equals(ModInt other) {
        return other.Modulus == this.Modulus 
            && other.Value == this.Value;
    }
    public override bool Equals(object obj) {
        return obj is ModInt && this.Equals((ModInt)obj);
    }
    public override int GetHashCode() {
        unchecked {
            return (Modulus.GetHashCode() * 3) ^ Value.GetHashCode();
        }
    }

    public override string ToString() {
        return Value + " (mod " + Modulus + ")";
    }

    public ModInt Zero { get { return new ModInt(0, this.Modulus); } }
    public ModInt One { get { return new ModInt(1 % this.Modulus, this.Modulus); } }

    public ModInt Plus(ModInt other) {
        return this + other;
    }

    public ModInt Times(ModInt other) {
        return this * other;
    }

    public ModInt AdditiveInverse { get { return -this; } }

    public string ListItemToString {
        get { return this.Value.ToString(); }
    }

    public string ListToStringSuffix {
        get { return " (mod " + this.Modulus.ToString() + ")"; }
    }

    public ModInt Random(ISecureRandomNumberGenerator rng) {
        return new ModInt(rng.GenerateNextValueMod(this.Modulus), this.Modulus);
    }


    public BigInteger FieldSize {
        get { return Modulus; }
    }


    public BigInteger ToInt() {
        return this.Value;
    }

    public ModInt FromInt(BigInteger i) {
        return Zero + i;
    }


    public bool IsZero {
        get { return this.Value == 0; }
    }

    public bool IsOne {
        get { return this.Value == 1 || this.Modulus == 1; }
    }


    public ModInt PlusOne() {
        return this + 1;
    }
}
