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

    private static ModInt Sum(IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a + e);
    }
    private static ModInt Product(IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a * e);
    }
    public static ShamirSecretShare Interpolate(IList<ShamirSecretShare> shares, ModInt targetX) {
        Contract.Requires(shares != null);
        Contract.Requires(shares.Any());
        Contract.Requires(shares.All(e => e.X != targetX));
        Contract.Requires(shares.All(e => e.Modulus == targetX.Modulus));
        Contract.Requires(shares.Select(e => e.X).Distinct().Count() == shares.Count);

        var targetY = Sum(shares.Select(e => {
            var otherShares = shares.Except(new[] { e });
            if (!otherShares.Any()) return e.Y;            
            var numerator = Product(otherShares.Select(f => targetX - f.X));
            var denominator = Product(otherShares.Select(f => e.X - f.X));
            return e.Y * numerator * denominator.MultiplicativeInverse();
        }));

        return new ShamirSecretShare(targetX, targetY);
    }
}
