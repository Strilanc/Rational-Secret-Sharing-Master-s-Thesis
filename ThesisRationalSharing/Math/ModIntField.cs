using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;

[DebuggerDisplay("{ToString()}")]
public class ModIntField : IFiniteField<ModInt>, IFiniteField<BigInteger> {
    public readonly BigInteger Modulus;
    public ModIntField(BigInteger modulus) {
        Contract.Requires(modulus > 0);
        this.Modulus = modulus;
    }
    public ModInt Add(ModInt value1, ModInt value2) { return value1 + value2; }
    public ModInt Multiply(ModInt value1, ModInt value2) { return value1 * value2; }
    public ModInt AdditiveInverse(ModInt value) { return -value; }
    public ModInt MultiplicativeInverse(ModInt value) { return value.MultiplicativeInverse; }
    public string ListItemToString(ModInt value) { return value.Value.ToString(); }
    public bool IsZero(ModInt value) { return value == 0; }
    public bool IsOne(ModInt value) { return value == 1 || Modulus == 1; }
    public ModInt Zero { get { return new ModInt(0, Modulus); } }
    public ModInt One { get { return Modulus == 0 ? Zero : new ModInt(1, Modulus); } }
    public string ListToStringSuffix { get { return " (mod " + Modulus + ")"; } }
    public override string ToString() { return "Integers mod " + Modulus; }

    public ModInt Random(ISecureRandomNumberGenerator rng) { return new ModInt(rng.GenerateNextValueMod(this.Modulus), this.Modulus); }
    public BigInteger Size { get { return Modulus; } }
    public BigInteger ToInt(ModInt value) { return value.Value; }
    public ModInt FromInt(BigInteger i) { return Zero + i; }

    public BigInteger Add(BigInteger value1, BigInteger value2) { return ModInt.From(value1 + value2, Modulus).Value; }
    public BigInteger Multiply(BigInteger value1, BigInteger value2) { return ModInt.From(value1 * value2, Modulus).Value; }
    public BigInteger AdditiveInverse(BigInteger value) { return ModInt.From(-value, Modulus).Value; }
    public BigInteger MultiplicativeInverse(BigInteger value) { return new ModInt(value, Modulus).MultiplicativeInverse.Value; }
    public string ListItemToString(BigInteger value) { return value.ToString(); }
    public bool IsZero(BigInteger value) { return value == 0; }
    public bool IsOne(BigInteger value) { return value == 1 || Modulus == 1; }
    BigInteger IField<BigInteger>.Zero { get { return 0; } }
    BigInteger IField<BigInteger>.One { get { return Modulus == 0 ? 0 : Modulus; } }

    BigInteger IFiniteField<BigInteger>.Random(ISecureRandomNumberGenerator rng) { return rng.GenerateNextValueMod(this.Modulus); }
    BigInteger IFiniteField<BigInteger>.ToInt(BigInteger value) { return value; }
    BigInteger IFiniteField<BigInteger>.FromInt(BigInteger i) { return i; }
}
