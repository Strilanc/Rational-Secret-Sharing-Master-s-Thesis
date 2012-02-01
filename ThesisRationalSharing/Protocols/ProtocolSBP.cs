using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class SBP<F, TVRFPub, TVRFPriv, TVRFProof> where F : IFiniteField<F>, IEquatable<F> {
        [DebuggerDisplay("{ToString()}")]
        public class Share {
            public readonly ICommitment<F> c; //commitment
            public readonly Dictionary<F, TVRFPub> V; //public vrf keys
            public readonly Dictionary<F, F> Y; //offsets
            public readonly F i; //share index
            public readonly TVRFPriv G; //private vrf key
            public Share(F i, ICommitment<F> c, Dictionary<F, TVRFPub> V, Dictionary<F, F> Y, TVRFPriv G) {
                this.i = i;
                this.c = c;
                this.V = V;
                this.G = G;
                this.Y = Y;
            }
            public override string ToString() {
                return "SBP Share " + i;
            }
        }

        public readonly int t;
        public readonly int n;
        public readonly F field;
        public readonly ICommitmentScheme<F> cs;
        public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
        public readonly Rational alpha;
        public SBP(int t, int n, F field, ICommitmentScheme<F> cs, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, Rational alpha) {
            this.t = t;
            this.n = n;
            this.field = field;
            this.cs = cs;
            this.vrfs = vrfs;
            this.alpha = alpha;
        }

        private static F[] ShareIndices(int n, F f) {
            var r = new F[n];
            r[0] = f.One;
            for (int i = 1; i < n; i++)
                r[i] = r[i - 1].Plus(f.One);
            return r;
        }
        public Share[] Deal(F secret, ISecureRandomNumberGenerator rng) {
            var indexes = ShareIndices(n, secret);
            
            var c = cs.Create(secret, rng);

            var vg = indexes.ToDictionary(i => i, i => vrfs.CreatePublicPrivateKeyPair(rng));
            var V = indexes.ToDictionary(i => i, i => vg[i].Item1);
            var G = indexes.ToDictionary(i => i, i => vg[i].Item2);
            
            var r = rng.GenerateNextValuePoisson(chanceContinue: 1 - alpha) + 1;

            var S = indexes.Zip(ShamirSecretSharing<F>.CreateShares(secret, t, n, rng), (e, i) => Tuple.Create(e, i)).ToDictionary(ei => ei.Item1, ei => ei.Item2);

            var Y = indexes.ToDictionary(i => i, i => S[i].Y.Plus(vrfs.Generate(G[i], r).Value.AdditiveInverse));

            return indexes.Select(i => new Share(i, c, V, Y, G[i])).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < t) throw new ArgumentException("Not enough shares");
            var r = 1;
            var c = availableShares.FirstOrDefault().c;
            while (true) {
                var M = availableShares.Select(e => vrfs.Generate(e.G, r)).ToArray();
                var S = availableShares.Zip(M, (e, m) => new Point<F>(e.i, m.Value.Plus(e.Y[e.i]))).ToArray();
                var s = ShamirSecretSharing<F>.TryCombineShares(t, S);
                if (s != null && c.Matches(s.Item1))
                    return s.Item1;
                r += 1;
            }
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, n, t, vrfs); }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, n, vrfs, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, n, vrfs, null); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                var messages = players.ToDictionary(e => e.Index, e => Tuple.Create(e.GetRoundMessage(r), e.GetRoundMessageReceivers()));
                var receivedMessages = new Dictionary<F, Dictionary<F, ProofValue<TVRFProof, F>>>();
                foreach (var receiver in players) {
                    var d = new Dictionary<F, ProofValue<TVRFProof, F>>();
                    receivedMessages[receiver.Index] = d;
                    foreach (var sender in players) {
                        if (messages[sender.Index].Item1 != null && messages[sender.Index].Item2.Contains(receiver.Index)) {
                            d[sender.Index] = messages[sender.Index].Item1;
                        }
                    }
                }
                foreach (var p in players)
                    p.UseRoundMessages(r, receivedMessages[p.Index]);
                r += 1;
            }
        }

        public interface IPlayer {
            F Index { get; }
            ProofValue<TVRFProof, F> GetRoundMessage(int round);
            IEnumerable<F> GetRoundMessageReceivers();
            void UseRoundMessages(int round, Dictionary<F, ProofValue<TVRFProof, F>> messages);
            Tuple<F> RecoveredSecretValue { get; }
            string DoneReason();
        }

        [DebuggerDisplay("{ToString()}")]
        public class RationalPlayer : IPlayer {
            public readonly Share share;
            public readonly HashSet<F> cooperatorIndexes = new HashSet<F>();
            public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
            public readonly int n;
            public readonly int t;
            private Tuple<F> secret = null;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue { get { return secret; } }

            public RationalPlayer(Share share, int n, int t, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs) {
                this.share = share;
                this.n = n;
                this.t = t;
                this.vrfs = vrfs;
                foreach (var fi in ShareIndices(n, share.i))
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (secret != null) return "Have secret";
                if (cooperatorIndexes.Count < t) return "Not enough cooperators";
                return null;
            }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (secret != null) return null;
                if (cooperatorIndexes.Count < t) return null;
                return vrfs.Generate(share.G, round);
            }
            public IEnumerable<F> GetRoundMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessages(int round, Dictionary<F, ProofValue<TVRFProof, F>> messages) {
                if (secret != null) return;

                var S = new List<Point<F>>();
                foreach (var m in messages) {
                    if (!cooperatorIndexes.Contains(m.Key)) continue;
                    if (!vrfs.Verify(share.V[m.Key], round, m.Value)) {
                        cooperatorIndexes.Remove(m.Key);
                        continue;
                    }
                    S.Add(new Point<F>(m.Key, m.Value.Value.Plus(share.Y[m.Key])));
                }
                cooperatorIndexes.IntersectWith(messages.Keys);
                if (cooperatorIndexes.Count < t) return;

                var s = ShamirSecretSharing<F>.TryCombineShares(t, S);
                if (s != null && share.c.Matches(s.Item1)) secret = s;
            }
            public override string ToString() { return "SBP Rational Player " + share.i; }
        }
        [DebuggerDisplay("{ToString()}")]
        public class MaliciousPlayer : IPlayer {
            public readonly Share share;
            public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
            public readonly ISecureRandomNumberGenerator randomMessageGenerator;
            public readonly IEnumerable<F> playerIndexes;
            public MaliciousPlayer(Share share, int n, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, ISecureRandomNumberGenerator randomMessageGenerator) { 
                this.share = share; 
                this.playerIndexes = ShareIndices(n, share.i);
                this.randomMessageGenerator = randomMessageGenerator;
                this.vrfs = vrfs;
            }
            public F Index { get { return share.i; } }
            public IEnumerable<F> GetRoundMessageReceivers() { return randomMessageGenerator == null ? new F[0] { } : playerIndexes; }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                return randomMessageGenerator == null ? null : vrfs.RandomMaliciousValue(randomMessageGenerator); 
            }
            public void UseRoundMessages(int round, Dictionary<F, ProofValue<TVRFProof, F>> messages) { }
            public override string ToString() { return "SBP Malicious Player " + share.i; }
        }

        public override string ToString() {
            return String.Format("SBP: n={0}, t={1}", n, t);
        }
    }
}
