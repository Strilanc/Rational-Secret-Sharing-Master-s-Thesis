using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class AsyncNoCrypto : ISecretSharingScheme<AsyncNoCrypto.Share> {
    public readonly BigInteger Modulus;

    public class PartialCommitment {
        private readonly BigInteger X;
        private readonly BigInteger Y;
        private readonly BigInteger Modulus;

        public PartialCommitment(BigInteger x, BigInteger y, BigInteger modulus) {
            Contract.Requires(x >= 0);
            Contract.Requires(y >= 0);
            Contract.Requires(modulus > x);
            Contract.Requires(modulus > y);
            this.X = x;
            this.Y = y;
            this.Modulus = modulus;
        }

        public static PartialCommitment FromPoly(ModIntPolynomial poly, ISecureRandomNumberGenerator rng) {
            Contract.Requires(rng != null);
            var x = rng.GenerateNextValueMod(poly.Modulus);
            var y = poly.EvaluateAt(x).Value;
            return new PartialCommitment(x, y, poly.Modulus);
        }

        public bool Matches(ModIntPolynomial poly) {
            return poly.Modulus == Modulus && poly.EvaluateAt(X) == Y;
        }
    }
    public AsyncNoCrypto(BigInteger modulus) {
        Contract.Requires(modulus > 0);
        this.Modulus = modulus;
    }

    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        var c1 = new Rational(5, 6);
        var c2 = new Rational(9, 10);
        var targetRound = 1 + rng.GenerateNextValueMod(total) + rng.GenerateNextValuePoisson(c1);
        var finalDataRound = targetRound + rng.GenerateNextValuePoisson(c2);
        ModInt r = new ModInt(0, Modulus);
        var roundShares = Enumerable.Range(0, (int)finalDataRound).Select(i => {
            var potentialSecret = i == targetRound ? (r + secret).Value : rng.GenerateNextValueMod(Modulus);
            var sharedValue = i == targetRound ? 0 : rng.GenerateNextValueMod(Modulus - 1) + 1;
            r += sharedValue;
            return CreateRound(potentialSecret, sharedValue, threshold, total, rng);
        }).ToArray();
        return Enumerable.Range(0, total).Select(i => new Share(roundShares.Select(rs => rs[i]).ToArray(), i)).ToArray();
    }
    private RoundShare[] CreateRound(
                BigInteger potentialSecret, 
                BigInteger sharedValue, 
                int threshold, 
                int total, 
                ISecureRandomNumberGenerator rng) {
        var sharesPoly = rng.GenerateNextModIntPolynomial(Modulus, threshold - 1, specifiedZero: sharedValue);
        var commitedShares = Enumerable.Range(0, total).Select(j => rng.GenerateNextModIntPolynomial(Modulus, threshold - 1, specifiedZero: sharesPoly.EvaluateAt(j + 1).Value)).ToArray();
        var commitments = commitedShares.Select(e => Enumerable.Range(0, total).Select(f => PartialCommitment.FromPoly(e, rng)).ToArray()).ToArray();
        return Enumerable.Range(0, total).Select(i => new RoundShare(commitedShares[i], commitments.Select(e => e[i]).ToArray())).ToArray();
    }

    public class RoundShare {
        public readonly BigInteger PotentialSecret;
        public readonly ModIntPolynomial CommittedShareValue;
        public readonly PartialCommitment[] Commitments;
        public RoundShare(ModIntPolynomial committedShareValue, PartialCommitment[] commitments) {
            this.CommittedShareValue = committedShareValue;
            this.Commitments = commitments;
        }
    }

    [DebuggerDisplay("{ToString()}")]
    public class Share {
        public readonly RoundShare[] RoundShares;
        public readonly int Index;
        public Share(RoundShare[] roundShares, int index) {
            this.RoundShares = roundShares;
            this.Index = index;
        }
        public override string ToString() {
            return String.Format("Id: {0}", Index);
        }
    }

    public BigInteger Combine(int degree, IList<Share> shares) {
        var r = TryCombine(degree, shares);
        if (r == null) throw new ArgumentException("Unrelated shares.");
        return r.Value;
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        if (shares.Select(e => e.Index).Duplicates().Any()) return null;
        if (shares.Select(e => e.RoundShares.Length).Distinct().Many()) return null;

        var shareMap = shares.ToDictionary(e => e.Index, e => e);
        
        var r = new ModInt(0, Modulus);
        for (int round = 0; round < shares.First().RoundShares.Length; round++) {
            var roundShares = shares.Select(e => Tuple.Create(e.Index, e.RoundShares[round])).ToArray();
            foreach (var rs in roundShares) {
                foreach (var vs in roundShares) {
                    if (!vs.Item2.Commitments[rs.Item1].Matches(rs.Item2.CommittedShareValue)) {
                        return null;
                    }
                }
            }
            var coords = roundShares.Select(e => Tuple.Create((BigInteger)e.Item1 + 1, e.Item2.CommittedShareValue.EvaluateAt(0).Value));
            
            var sharedValue = ModIntPolynomial.FromInterpolation(coords, Modulus).EvaluateAt(0);
            var potentialSecret = roundShares.Select(e => e.Item2.PotentialSecret).Distinct().Single();

            if (sharedValue == 0) return (r + potentialSecret).Value;
            r += sharedValue;            
        }
        return null;
    }
}
