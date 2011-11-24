using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class AsyncNoCrypto : ISecretSharingScheme<AsyncNoCrypto.Share> {
    public readonly BigInteger Modulus;
    public readonly Rational MarginalChanceDelayTargetRound;
    public readonly Rational MarginalChanceAppendFakeRound;

    public AsyncNoCrypto(BigInteger modulus, Rational marginalChanceDelayTargetRound, Rational marginalChanceAppendFakeRound) {
        Contract.Requires(modulus > 0);
        Contract.Requires(marginalChanceDelayTargetRound >= 0);
        Contract.Requires(marginalChanceDelayTargetRound < 1);
        Contract.Requires(marginalChanceAppendFakeRound >= 0);
        Contract.Requires(marginalChanceAppendFakeRound < 1);
        this.Modulus = modulus;
        this.MarginalChanceDelayTargetRound = marginalChanceDelayTargetRound;
        this.MarginalChanceAppendFakeRound = marginalChanceAppendFakeRound;
    }

    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        var targetRound = 1 + rng.GenerateNextValueMod(total) + rng.GenerateNextValuePoisson(MarginalChanceDelayTargetRound);
        var finalDataRound = targetRound + rng.GenerateNextValuePoisson(MarginalChanceAppendFakeRound);
        var r = new ModInt(0, Modulus);
        var roundShares = Enumerable.Range(0, (int)finalDataRound + 1).Select(i => {
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
        var spreadShares = Enumerable.Range(0, total).Select(j => PolyCommitment.Spread(sharesPoly.EvaluateAt(j + 1), threshold - 1, rng)).ToArray();
        var commitments = spreadShares.Select(e => Enumerable.Range(0, total).Select(f => PolyCommitment.FromPoly(e, rng)).ToArray()).ToArray();
        return Enumerable.Range(0, total).Select(i => new RoundShare(potentialSecret, spreadShares[i], commitments.Select(e => e[i]).ToArray())).ToArray();
    }

    public class RoundShare {
        public readonly BigInteger PotentialSecret;
        public readonly ModIntPolynomial CommittedShareValue;
        public readonly PolyCommitment[] Commitments;
        public RoundShare(BigInteger potentialSecret, ModIntPolynomial committedShareValue, PolyCommitment[] commitments) {
            this.PotentialSecret = potentialSecret;
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
            var coords = roundShares.Select(e => Tuple.Create((BigInteger)e.Item1 + 1, PolyCommitment.Merge(e.Item2.CommittedShareValue).Value));
            
            var sharedValue = ModIntPolynomial.FromInterpolation(coords, Modulus).EvaluateAt(0);
            var potentialSecret = roundShares.Select(e => e.Item2.PotentialSecret).Distinct().Single();

            if (sharedValue == 0) return (potentialSecret - r).Value;
            r += sharedValue;            
        }
        return null;
    }

    public BigInteger Range {
        get { return Modulus; }
    }
}
