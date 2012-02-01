using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class ABIP<F, TVRFPub, TVRFPriv, TVRFProof> where F : IFiniteField<F>, IEquatable<F> {
        [DebuggerDisplay("{ToString()}")]
        public class Share {
            public readonly Dictionary<F, TVRFPub> V; //public vrf keys
            public readonly Dictionary<F, F> Y; //offsets
            public readonly F i; //share index
            public readonly TVRFPriv G; //private vrf key
            public Share(F i, Dictionary<F, TVRFPub> V, Dictionary<F, F> Y, TVRFPriv G) {
                this.i = i;
                this.V = V;
                this.G = G;
                this.Y = Y;
            }
            public override string ToString() {
                return "ABIP Share " + i;
            }
        }

        public readonly int t;
        public readonly int n;
        public readonly F field;
        public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
        public readonly Rational alpha;
        public ABIP(int t, int n, F field, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, Rational alpha) {
            this.t = t;
            this.n = n;
            this.field = field;
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

            var r = rng.GenerateNextValuePoisson(chanceContinue: 1 - alpha) + 1;

            var vg = indexes.ToDictionary(i => i, i => vrfs.CreatePublicPrivateKeyPair(rng));
            var V = indexes.ToDictionary(i => i, i => vg[i].Item1);
            var G = indexes.ToDictionary(i => i, i => vg[i].Item2);
            
            var S = indexes.Zip(ShamirSecretSharing<F>.CreateShares(secret, t - 1, n, rng), (e, i) => Tuple.Create(e, i)).ToDictionary(ei => ei.Item1, ei => ei.Item2);

            var Y = indexes.ToDictionary(i => i, i => S[i].Y.Minus(vrfs.Generate(G[i], r).Value));

            return indexes.Select(i => new Share(i, V, Y, G[i])).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < t) throw new ArgumentException("Not enough shares");
            var r = 1;
            var zero = availableShares.First().Y.First().Value.Zero;
            while (true) {
                var M = availableShares.Select(e => vrfs.Generate(e.G, r)).ToArray();
                var S = availableShares.Zip(M, (e, m) => new Point<F>(e.i, m.Value.Plus(e.Y[e.i]))).ToArray();
                var p = Polynomial<F>.FromInterpolation(S);
                if (p.Degree <= t - 1) return p.EvaluateAt(zero);
                r += 1;
            }
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, new Share[0], n, t, vrfs); }
        public IPlayer[] MakeCooperateUntilLearnCoalition(Share[] shares) {
            return shares.Select(s => new RationalPlayer(s, shares, n, t, vrfs)).ToArray();
        }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, n, vrfs, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, n, vrfs, null); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                foreach (var p in players)
                    p.StartRound(r);
                for (int t = 1; t <= n; t++) {
                    var sender = players.SingleOrDefault(e => e.Index.ToInt() == t);
                    var message = sender == null ? null : sender.GetRoundMessage(r);
                    var receivers = sender == null ? new F[0] : sender.GetMessageReceivers();
                    foreach (var p in players)
                        p.UseTurnMessage(r, t, sender.Index, receivers.Contains(p.Index) ? message : null);
                }
                r += 1;
            }
        }

        public interface IPlayer {
            F Index { get; }
            ProofValue<TVRFProof, F> GetRoundMessage(int round);
            IEnumerable<F> GetMessageReceivers();
            void UseTurnMessage(int round, int turn, F senderId, ProofValue<TVRFProof, F> message);
            Tuple<F> RecoveredSecretValue { get; }
            string DoneReason();
            void StartRound(int round);
        }

        [DebuggerDisplay("{ToString()}")]
        public class RationalPlayer : IPlayer {
            public readonly Share share;
            public readonly Share[] coalitionShares;
            public readonly HashSet<F> cooperatorIndexes = new HashSet<F>();
            private int lastRound = 0;
            public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
            public readonly int n;
            public readonly int t;
            private Tuple<F> secret = null;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue { 
                get { 
                    if (secret != null) return secret;
                    if (receivedShares.Count == t - 1 && lastRound.ProperMod(n) == 0)
                        return Tuple.Create(Polynomial<F>.FromInterpolation(receivedShares.Values).EvaluateAt(share.i.Zero));
                    return null;
                } 
            }
            private readonly Dictionary<F, Point<F>> receivedShares = new Dictionary<F, Point<F>>();

            public RationalPlayer(Share share, Share[] coalitionShares, int n, int t, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs) {
                this.share = share;
                this.n = n;
                this.t = t;
                this.vrfs = vrfs;
                this.coalitionShares = coalitionShares;
                foreach (var fi in ShareIndices(n, share.i))
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (secret != null) return "Have secret";
                if (cooperatorIndexes.Count < t) return "Not enough cooperators";
                if (cooperatorIndexes.Count == t-1 && lastRound.ProperMod(n) == 0) return "Recovery";
                return null;
            }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (secret != null) return null;
                if (cooperatorIndexes.Count < t) return null;
                return vrfs.Generate(share.G, round);
            }
            public void StartRound(int round) {
                receivedShares.Clear();
                foreach (var s in coalitionShares.Concat(new[] { share })) {
                    receivedShares[s.i] = new Point<F>(s.i, vrfs.Generate(s.G, round).Value.Plus(share.Y[s.i]));
                }
            }
            public IEnumerable<F> GetMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseTurnMessage(int round, int turn, F senderId, ProofValue<TVRFProof, F> message) {
                if (message == null) cooperatorIndexes.Remove(senderId);
                if (secret != null) return;
                if (!cooperatorIndexes.Contains(senderId)) return;
                if (!vrfs.Verify(share.V[senderId], round, message)) {
                    cooperatorIndexes.Remove(senderId);
                    return;
                }
                receivedShares[senderId] = new Point<F>(senderId, message.Value.Plus(share.Y[senderId]));
                if (receivedShares.Count != t) return;
                
                var s = Polynomial<F>.FromInterpolation(receivedShares.Values);
                if (s.Degree < t - 1) secret = Tuple.Create(s.EvaluateAt(senderId.Zero));
            }
            public override string ToString() { return "ABIP Rational Player " + share.i; }
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
            public F GetRoundExpectedSender(int round) {
                return this.share.Y.First().Value.FromInt((round - 1) % playerIndexes.Count() + 1);
            }
            public IEnumerable<F> GetMessageReceivers() { return randomMessageGenerator == null ? new F[0] { } : playerIndexes; }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) { 
                return randomMessageGenerator == null ? null : vrfs.RandomMaliciousValue(randomMessageGenerator); 
            }
            public override string ToString() { return "ABIP Malicious Player " + share.i; }
            public void StartRound(int round) { }
            public void UseTurnMessage(int round, int turn, F senderId, ProofValue<TVRFProof, F> message) {
            }
        }

        public override string ToString() {
            return String.Format("ABIP: n={0}, t={1}", n, t);
        }
    }
}
