using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public struct ShamirSecretShare {
    public readonly ModInt X;
    public readonly ModInt Y;
    public BigInteger Modulus { get { return X.Modulus; } }

    public ShamirSecretShare(ModInt x, ModInt y) {
        Contract.Requires(x.Modulus == y.Modulus);
        this.X = x;
        this.Y = y;
    }
    public static ShamirSecretShare From(BigInteger x, BigInteger y, BigInteger modulus) {
        return new ShamirSecretShare(ModInt.From(x, modulus), ModInt.From(y, modulus));
    }
    public static ShamirSecretShare FromPoly(ModIntPolynomial poly, BigInteger x) {
        return new ShamirSecretShare(ModInt.From(x, poly.Modulus), poly.EvaluateAt(x));
    }

    private static ModInt Sum(IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a + e);
    }
    private static ModInt Product(IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a * e);
    }
    public static ModIntPolynomial InterpolatePoly(IList<ShamirSecretShare> shares) {
        Contract.Requires(shares != null);
        Contract.Requires(shares.Any());
        Contract.Requires(shares.All(e => e.Modulus == shares.First().Modulus));
        Contract.Requires(shares.Select(e => e.X).Distinct().Count() == shares.Count);
        return ModIntPolynomial.FromInterpolation(shares.Select(e => Tuple.Create(e.X.Value, e.Y.Value)).ToArray(), shares.First().Modulus);
    }
    public static BigInteger InterpolateSecret(IList<ShamirSecretShare> shares) {
        Contract.Requires(shares != null);
        Contract.Requires(shares.Any());
        Contract.Requires(shares.All(e => e.Modulus == shares.First().Modulus));
        Contract.Requires(shares.Select(e => e.X).Distinct().Count() == shares.Count);
        return InterpolatePoly(shares).EvaluateAt(0).Value;
    }
}
