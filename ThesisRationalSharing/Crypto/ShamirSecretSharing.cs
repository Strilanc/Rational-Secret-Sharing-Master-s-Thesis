using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class ShamirSecretSharing : ISharingScheme<ShamirSecretSharing.Share> {
    public readonly BigInteger Modulus;

    public ShamirSecretSharing(BigInteger modulus) {
        this.Modulus = modulus;
    }

    private IEnumerable<Share> GenerateShares(BigInteger secret, int threshold, ISecureRandomNumberGenerator r) {
        var coefficients = new BigInteger[threshold];
        coefficients[0] = secret;
        for (int i = 1; i < threshold; i++) {
            coefficients[i] = r.GenerateNextValueMod(Modulus);
        }
        var poly = ModIntPolynomial.From(coefficients, Modulus);

        for (var i = BigInteger.One; i < Modulus; i++) {
            var x = new ModInt(i, Modulus);
            var y = poly.EvaluateAt(x);
            yield return new Share(x, y);
        }
    }
    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator r) {
        return GenerateShares(secret, threshold, r).Take(total).ToArray();
    }

    public BigInteger Combine(int degree, IList<Share> shares) {
        var r = TryCombine(degree, shares);
        if (r == null) throw new ArgumentException("Inconsistent shares.");
        return r.Value;
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        var poly = InterpolatePoly(shares.Take(degree).ToArray());
        if (shares.Any(e => poly.EvaluateAt(e.X) != e.Y)) return null;
        return poly.EvaluateAt(0).Value;
    }

    [DebuggerDisplay("{ToString()}")]
    public struct Share {
        public readonly ModInt X;
        public readonly ModInt Y;
        public BigInteger Modulus { get { return X.Modulus; } }

        public Share(ModInt x, ModInt y) {
            Contract.Requires(x.Modulus == y.Modulus);
            this.X = x;
            this.Y = y;
        }
        public static Share From(BigInteger x, BigInteger y, BigInteger modulus) {
            return new Share(ModInt.From(x, modulus), ModInt.From(y, modulus));
        }
        public static Share FromPoly(ModIntPolynomial poly, BigInteger x) {
            return new Share(ModInt.From(x, poly.Modulus), poly.EvaluateAt(x));
        }
        public override string ToString() {
            return "P(" + X.Value + ") = " + Y.Value + " (mod " + Modulus + ")";
        }
    }

    public static ModIntPolynomial InterpolatePoly(IList<Share> shares) {
        Contract.Requires(shares != null);
        Contract.Requires(shares.Any());
        Contract.Requires(shares.All(e => e.Modulus == shares.First().Modulus));
        Contract.Requires(shares.Select(e => e.X).Distinct().Count() == shares.Count);
        return ModIntPolynomial.FromInterpolation(shares.Select(e => Tuple.Create(e.X.Value, e.Y.Value)).ToArray(), shares.First().Modulus);
    }
}
