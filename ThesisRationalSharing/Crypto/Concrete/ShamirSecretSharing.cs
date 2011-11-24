using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class ShamirSecretSharing : ISecretSharingScheme<ModPoint> {
    public readonly BigInteger Modulus;

    public ShamirSecretSharing(BigInteger modulus) {
        this.Modulus = modulus;
    }

    private static IEnumerable<ModPoint> GenerateShares(ModInt secret, int threshold, ISecureRandomNumberGenerator r) {
        var poly = r.GenerateNextModIntPolynomial(secret.Modulus, degree: threshold - 1, specifiedZero: secret.Value);

        for (var i = BigInteger.One; i < secret.Modulus; i++) {
            yield return ModPoint.FromPoly(poly, i);
        }
    }
    private IEnumerable<ModPoint> GenerateShares(BigInteger secret, int threshold, ISecureRandomNumberGenerator r) {
        return GenerateShares(ModInt.From(secret, Range), threshold, r);
    }
    public ModPoint[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator r) {
        return GenerateShares(secret, threshold, r).Take(total).ToArray();
    }
    public static ModPoint[] Create(ModInt secret, int threshold, int total, ISecureRandomNumberGenerator r) {
        if (!secret.Modulus.IsLikelyPrime(r)) throw new InvalidOperationException();
        return GenerateShares(secret, threshold, r).Take(total).ToArray();
    }

    public static BigInteger CombineShares(int degree, IList<ModPoint> shares) {
        var r = TryCombineShares(degree, shares);
        if (r == null) throw new ArgumentException("Inconsistent shares.");
        return r.Value;
    }
    public static BigInteger? TryCombineShares(int degree, IList<ModPoint> shares) {
        if (shares.Count < degree) return null;
        var poly = InterpolatePoly(shares.Take(degree).ToArray());
        if (shares.Any(e => poly.EvaluateAt(e.X) != e.Y)) return null;
        return poly.EvaluateAt(0).Value;
    }
    public BigInteger Combine(int degree, IList<ModPoint> shares) {
        return CombineShares(degree, shares);
    }
    public BigInteger? TryCombine(int degree, IList<ModPoint> shares) {
        return TryCombineShares(degree, shares);
    }

    public static ModIntPolynomial InterpolatePoly(IList<ModPoint> shares) {
        Contract.Requires(shares != null);
        Contract.Requires(shares.Any());
        Contract.Requires(shares.Select(e => e.Modulus).Distinct().IsSingle());
        Contract.Requires(shares.Select(e => e.X).Duplicates().None());
        return ModIntPolynomial.FromInterpolation(shares.Select(e => Tuple.Create(e.X.Value, e.Y.Value)).ToArray(), shares.First().Modulus);
    }

    public BigInteger Range { get { return Modulus; } }
}
