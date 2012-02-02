using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class ABCP<F, TVRFPub, TVRFPriv, TVRFProof> where F : IFiniteField<F>, IEquatable<F> {
        [DebuggerDisplay("{ToString()}")]
        public class Share {
            public readonly Dictionary<F, TVRFPub> V; //public vrf keys
            public readonly Dictionary<F, F> Y; //offsets
            public readonly ICommitment<F> c;
            public readonly F i; //share index
            public readonly TVRFPriv G; //private vrf key
            public Share(F i, ICommitment<F> c, Dictionary<F, TVRFPub> V, Dictionary<F, F> Y, TVRFPriv G) {
                this.c = c;
                this.i = i;
                this.V = V;
                this.G = G;
                this.Y = Y;
            }
            public override string ToString() {
                return "ABCP Share " + i;
            }
        }

        public readonly int t;
        public readonly int n;
        public readonly F field;
        public readonly int delta;
        public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
        public readonly ICommitmentScheme<F> cs;
        public readonly Rational alpha;
        public ABCP(int t, int n, F field, int delta, ICommitmentScheme<F> cs, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, Rational alpha) {
            this.t = t;
            this.n = n;
            this.delta = delta;
            this.field = field;
            this.vrfs = vrfs;
            this.alpha = alpha;
            this.cs = cs;
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

            var b = rng.GenerateNextValueMod(n) + rng.GenerateNextValuePoisson(chanceContinue: 1 - alpha) + 1;
            var r = indexes.ToDictionary(i => i, i => b + ((i.ToInt() - b + t + delta + 1).ProperMod(n)));

            var vg = indexes.ToDictionary(i => i, i => vrfs.CreatePublicPrivateKeyPair(rng));
            var V = indexes.ToDictionary(i => i, i => vg[i].Item1);
            var G = indexes.ToDictionary(i => i, i => vg[i].Item2);
            
            var S = indexes.ToDictionary(x => x, x => indexes.Zip(ShamirSecretSharing<F>.CreateShares(secret, t - 1, n, rng), (e, i) => Tuple.Create(e, i)).ToDictionary(ei => ei.Item1, ei => ei.Item2));

            var Y = indexes.ToDictionary(i => i, i => indexes.ToDictionary(j => j, j => S[i][j].Y.Minus(vrfs.Generate(G[j], r[i] + (j.ToInt() - r[i]).ProperMod(n)).Value)));

            return indexes.Select(i => new Share(i, c, V, Y[i], G[i])).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < t) throw new ArgumentException("Not enough shares");
            var r = 1;
            var zero = availableShares.First().Y.First().Value.Zero;
            var cx = availableShares.First();
            var Q = new Queue<Point<F>>();
            while (true) {
                try {
                    var sender = availableShares.SingleOrDefault(e => e.i.ToInt().ProperMod(n) == r.ProperMod(n));
                    if (sender == null) continue;
                    var m = vrfs.Generate(sender.G, r);
                    Q.Enqueue(new Point<F>(sender.i, m.Value.Plus(cx.Y[sender.i])));
                    if (Q.Count > n) Q.Dequeue();
                    var p = Polynomial<F>.FromInterpolation(Q);
                    var s = p.EvaluateAt(field.Zero);
                    if (cx.c.Matches(s)) return s;
                } finally {
                    r += 1;
                }
            }
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, new Share[0], n, t, vrfs, new Tuple<F>[1]); }
        public IPlayer[] MakeCooperateUntilLearnCoalition(Share[] shares) {
            var x = new Tuple<F>[1];
            return shares.Select(s => new RationalPlayer(s, shares, n, t, vrfs, x)).ToArray();
        }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, n, vrfs, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, n, vrfs, null); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                var sender = players.SingleOrDefault(e => e.Index.ToInt().ProperMod(n) == r.ProperMod(n));
                var message = sender == null ? null : sender.GetRoundMessage(r);
                var receivers = sender == null ? new F[0] : sender.GetMessageReceivers();
                foreach (var p in players)
                    p.UseRoundMessage(r, sender.Index, receivers.Contains(p.Index) ? message : null);
                r += 1;
            }
        }

        public interface IPlayer {
            F Index { get; }
            ProofValue<TVRFProof, F> GetRoundMessage(int round);
            IEnumerable<F> GetMessageReceivers();
            void UseRoundMessage(int round, F senderId, ProofValue<TVRFProof, F> message);
            Tuple<F> RecoveredSecretValue { get; }
            string DoneReason();
        }

        [DebuggerDisplay("{ToString()}")]
        public class RationalPlayer : IPlayer {
            public readonly Share share;
            public readonly Share[] coalitionShares;
            public readonly HashSet<F> cooperatorIndexes = new HashSet<F>();
            public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
            public readonly int n;
            public readonly int t;
            private Tuple<F>[] secret;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue {
                get {
                    return secret[0];
                } 
            }
            private readonly Queue<Point<F>> receivedShares = new Queue<Point<F>>();

            public RationalPlayer(Share share, Share[] coalitionShares, int n, int t, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, Tuple<F>[] secret) {
                Contract.Requires(secret.Length == 1);
                this.share = share;
                this.n = n;
                this.t = t;
                this.vrfs = vrfs;
                this.coalitionShares = coalitionShares;
                this.secret = secret;
                foreach (var fi in ShareIndices(n, share.i))
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (secret[0] != null) return "Have secret";
                if (cooperatorIndexes.Count < t) return "Not enough cooperators";
                return null;
            }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (secret[0] != null) return null;
                if (cooperatorIndexes.Count < t) return null;
                return vrfs.Generate(share.G, round);
            }
            public IEnumerable<F> GetMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessage(int round, F senderId, ProofValue<TVRFProof, F> message) {
                if (message == null) cooperatorIndexes.Remove(senderId);
                if (secret[0] != null) return;
                if (!cooperatorIndexes.Contains(senderId)) return;
                if (!vrfs.Verify(share.V[senderId], round, message)) {
                    cooperatorIndexes.Remove(senderId);
                    return;
                }
                if (cooperatorIndexes.Count < t) return;
                receivedShares.Enqueue(new Point<F>(senderId, message.Value.Plus(share.Y[senderId])));
                if (coalitionShares != null) SimulateCoalition(round);
                if (receivedShares.Count > t) receivedShares.Dequeue();                
                var s = Polynomial<F>.FromInterpolation(receivedShares).EvaluateAt(share.i.Zero);
                if (share.c.Matches(s)) secret[0] = Tuple.Create(s);
            }
            private void SimulateCoalition(int curRound) {
                var q = new Queue<Point<F>>(receivedShares);
                for (int r = 1; r < n; r++) {
                    var simRound = curRound + r;
                    if (q.Count > 0 && q.First().X.ToInt().ProperMod(n) == simRound.ProperMod(n))
                        q.Dequeue();
                    var shr = coalitionShares.SingleOrDefault(e => e.i.ToInt().ProperMod(n) == simRound.ProperMod(n));
                    if (shr == null) continue;
                    q.Enqueue(new Point<F>(shr.i, vrfs.Generate(shr.G, simRound).Value.Plus(share.Y[shr.i])));
                    if (q.Count > t) receivedShares.Dequeue();
                    var s = Polynomial<F>.FromInterpolation(q).EvaluateAt(share.i.Zero);
                    if (share.c.Matches(s)) secret[0] = Tuple.Create(s);
                }
            }
            public override string ToString() { return "ABCP Rational Player " + share.i; }
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
            public override string ToString() { return "ABCP Malicious Player " + share.i; }
            public void UseRoundMessage(int round, F senderId, ProofValue<TVRFProof, F> message) {
            }
        }

        public override string ToString() {
            return String.Format("ABCP: n={0}, t={1}", n, t);
        }
    }
}
