using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

[DebuggerDisplay("{ToString()}")]
public class PolyCommitment {
    private readonly BigInteger X;
    private readonly BigInteger Y;
    private readonly BigInteger Modulus;

    public PolyCommitment(BigInteger x, BigInteger y, BigInteger modulus) {
        Contract.Requires(x >= 0);
        Contract.Requires(y >= 0);
        Contract.Requires(modulus > x);
        Contract.Requires(modulus > y);
        this.X = x;
        this.Y = y;
        this.Modulus = modulus;
    }

    public static ModIntPolynomial Spread(ModInt value, int degree, ISecureRandomNumberGenerator rng) {
        var m = value.Modulus;
        var poly = rng.GenerateNextModIntPolynomial(m, degree, specifiedZero: 0);
        var s = poly.GetCoefficients().Any() ? poly.GetCoefficients().Sum() : new ModInt(0, m);
        return poly + ModIntPolynomial.FromCoefficients(new[] { value - s }, m);
    }
    public static ModInt Merge(ModIntPolynomial poly) {
        return poly.GetCoefficients().Any() ? poly.GetCoefficients().Sum() : new ModInt(0, poly.Modulus);
    }

    public static PolyCommitment FromPoly(ModIntPolynomial poly, ISecureRandomNumberGenerator rng) {
        Contract.Requires(rng != null);
        var x = rng.GenerateNextValueMod(poly.Modulus);
        var y = poly.EvaluateAt(x).Value;
        return new PolyCommitment(x, y, poly.Modulus);
    }

    public bool Matches(ModIntPolynomial poly) {
        return poly.Modulus == Modulus && poly.EvaluateAt(X) == Y;
    }

    public override string ToString() {
        return String.Format("P({0}) == {1} (mod {2})", X, Y, Modulus);
    }
}
