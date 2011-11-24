using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Points of modular integers.</summary>
[DebuggerDisplay("{ToString()}")]
public struct ModPoint : IEquatable<ModPoint> {
    private readonly BigInteger _X;
    private readonly BigInteger _Y;
    public ModInt X { get { return new ModInt(_X, Modulus); } }
    public ModInt Y { get { return new ModInt(_Y, Modulus); } }
    public readonly BigInteger Modulus;

    public ModPoint(BigInteger x, BigInteger y, BigInteger modulus) {
        Contract.Requires(x >= 0);
        Contract.Requires(y >= 0);
        Contract.Requires(modulus > x);
        Contract.Requires(modulus > y);
        this._X = x;
        this._Y = y;
        this.Modulus = modulus;
    }

    public override int GetHashCode() {
        unchecked {
            return _X.GetHashCode() ^ (_Y.GetHashCode() * 3) ^ (Modulus.GetHashCode() * 5);
        }
    }
    public override string ToString() {
        return String.Format("({0}, {1}) (mod {2})", _X, _Y, Modulus);
    }
    public override bool Equals(object obj) {
        return obj is ModPoint && this.Equals((ModPoint)obj);
    }
    public bool Equals(ModPoint other) {
        return this._X == other._X && this._Y == other._Y && this.Modulus == other.Modulus;
    }
    public static bool operator ==(ModPoint value1, ModPoint value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(ModPoint value1, ModPoint value2) {
        return !value1.Equals(value2);
    }

    public static ModPoint From(BigInteger x, BigInteger y, BigInteger modulus) {
        return new ModPoint(ModInt.From(x, modulus).Value, ModInt.From(y, modulus).Value, modulus);
    }
    public static ModPoint From(ModInt x, ModInt y) {
        Contract.Requires(x.Modulus == y.Modulus);
        return new ModPoint(x.Value, y.Value, x.Modulus);
    }
    public static ModPoint From(BigInteger x, ModInt y) {
        return new ModPoint(x, y.Value, y.Modulus);
    }
    public static ModPoint From(ModInt x, BigInteger y) {
        return new ModPoint(x.Value, y, x.Modulus);
    }
    public static ModPoint FromPoly(ModIntPolynomial poly, BigInteger x) {
        return new ModPoint(ModInt.From(x, poly.Modulus).Value, poly.EvaluateAt(x).Value, poly.Modulus);
    }
}
