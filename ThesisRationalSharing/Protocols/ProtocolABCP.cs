using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class ABCP<F, TVRFPub, TVRFPriv, TVRFProof> {
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
        public readonly IFiniteField<F> field;
        public readonly int delta;
        public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs;
        public readonly ICommitmentScheme<F> cs;
        public readonly Rational alpha;
        public ABCP(int t, int n, IFiniteField<F> field, int delta, ICommitmentScheme<F> cs, IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> vrfs, Rational alpha) {
            this.t = t;
            this.n = n;
            this.delta = delta;
            this.field = field;
            this.vrfs = vrfs;
            this.alpha = alpha;
            this.cs = cs;
        }

        private F[] ShareIndices() {
            var r = new F[this.n];
            r[0] = field.One;
            for (int i = 1; i < n; i++)
                r[i] = field.Plus(r[i - 1], field.One);
            return r;
        }
        public Share[] Deal(F secret, ISecureRandomNumberGenerator rng) {
            var indexes = ShareIndices();
            var c = cs.Create(secret, rng);

            var b = rng.GenerateNextValueMod(n) + rng.GenerateNextValueGeometric(chanceStop: alpha, min: 1);
            var r = indexes.MapTo(i => b + ((field.ToInt(i) - b + t + delta + 1).ProperMod(n)));

            var vg = indexes.MapTo(i => vrfs.CreatePublicPrivateKeyPair(rng));
            var V = indexes.MapTo(i => vg[i].Item1);
            var G = indexes.MapTo(i => vg[i].Item2);

            var S = indexes.MapTo(i => ShamirSecretSharing.CreateShares(field, secret, t, n, rng).KeyBy(e => e.X));

            var Y = indexes.MapTo(i => indexes.ToDictionary(j => j, j => field.Minus(S[i][j].Y, vrfs.Generate(G[j], r[i] + (field.ToInt(j) - r[i]).ProperMod(n)).Value)));

            return indexes.Select(i => new Share(i, c, V, Y[i], G[i])).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < t) throw new ArgumentException("Not enough shares");
            var r = 1;
            var cx = availableShares.First();
            var Q = new Queue<Point<F>>();
            while (true) {
                try {
                    var sender = availableShares.SingleOrDefault(e => field.ToInt(e.i).ProperMod(n) == r.ProperMod(n));
                    if (sender == null) continue;
                    var m = vrfs.Generate(sender.G, r);
                    Q.Enqueue(new Point<F>(field, sender.i, field.Plus(m.Value, cx.Y[sender.i])));
                    if (Q.Count > n) Q.Dequeue();
                    var p = Polynomial<F>.FromInterpolation(field, Q);
                    var s = p.EvaluateAt(field.Zero);
                    if (cx.c.Matches(s)) return s;
                } finally {
                    r += 1;
                }
            }
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, new Share[0], this, new Tuple<F>[1]); }
        public IPlayer[] MakeCooperateUntilLearnCoalition(Share[] shares) {
            var x = new Tuple<F>[1];
            return shares.Select(s => new RationalPlayer(s, shares, this, x)).ToArray();
        }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, this, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, this, null); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                var sender = players.SingleOrDefault(e => field.ToInt(e.Index).ProperMod(n) == r.ProperMod(n));
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
            public readonly ABCP<F, TVRFPub, TVRFPriv, TVRFProof> scheme;
            private Tuple<F>[] coalitionSecretPointer;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue {
                get {
                    return coalitionSecretPointer[0];
                } 
            }
            private readonly Queue<Point<F>> receivedShares = new Queue<Point<F>>();

            public RationalPlayer(Share share, Share[] coalitionShares, ABCP<F, TVRFPub, TVRFPriv, TVRFProof> scheme, Tuple<F>[] coalitionSharedSecretPointer) {
                Contract.Requires(coalitionSharedSecretPointer.Length == 1);
                this.share = share;
                this.scheme = scheme;
                this.coalitionShares = coalitionShares;
                this.coalitionSecretPointer = coalitionSharedSecretPointer;
                foreach (var fi in scheme.ShareIndices())
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (coalitionSecretPointer[0] != null) return "Have secret";
                if (cooperatorIndexes.Count < scheme.t) return "Not enough cooperators";
                return null;
            }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (coalitionSecretPointer[0] != null) return null;
                if (cooperatorIndexes.Count < scheme.t) return null;
                return scheme.vrfs.Generate(share.G, round);
            }
            public IEnumerable<F> GetMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessage(int round, F senderId, ProofValue<TVRFProof, F> message) {
                if (message == null) cooperatorIndexes.Remove(senderId);
                if (coalitionSecretPointer[0] != null) return;
                if (!cooperatorIndexes.Contains(senderId)) return;
                if (!scheme.vrfs.Verify(share.V[senderId], round, message)) {
                    cooperatorIndexes.Remove(senderId);
                    return;
                }
                if (cooperatorIndexes.Count < scheme.t) return;
                receivedShares.Enqueue(new Point<F>(scheme.field, senderId, scheme.field.Plus(message.Value, share.Y[senderId])));
                if (coalitionShares != null) SimulateCoalition(round);
                if (receivedShares.Count > scheme.t) receivedShares.Dequeue();                
                var s = Polynomial<F>.FromInterpolation(scheme.field, receivedShares).EvaluateAt(scheme.field.Zero);
                if (share.c.Matches(s)) coalitionSecretPointer[0] = Tuple.Create(s);
            }
            private void SimulateCoalition(int curRound) {
                var q = new Queue<Point<F>>(receivedShares);
                for (int r = 1; r < scheme.n; r++) {
                    var simRound = curRound + r;
                    if (q.Count > 0 && scheme.field.ToInt(q.First().X).ProperMod(scheme.n) == simRound.ProperMod(scheme.n))
                        q.Dequeue();
                    var shr = coalitionShares.SingleOrDefault(e => scheme.field.ToInt(e.i).ProperMod(scheme.n) == simRound.ProperMod(scheme.n));
                    if (shr == null) continue;
                    q.Enqueue(new Point<F>(scheme.field, shr.i, scheme.field.Plus(scheme.vrfs.Generate(shr.G, simRound).Value, share.Y[shr.i])));
                    if (q.Count > scheme.t) receivedShares.Dequeue();
                    var s = Polynomial<F>.FromInterpolation(scheme.field, q).EvaluateAt(scheme.field.Zero);
                    if (share.c.Matches(s)) coalitionSecretPointer[0] = Tuple.Create(s);
                }
            }
            public override string ToString() { return "ABCP Rational Player " + share.i; }
        }
        [DebuggerDisplay("{ToString()}")]
        public class MaliciousPlayer : IPlayer {
            public readonly Share share;
            public readonly ABCP<F, TVRFPub, TVRFPriv, TVRFProof> scheme;
            public readonly ISecureRandomNumberGenerator randomMessageGenerator;
            public readonly IEnumerable<F> playerIndexes;
            public MaliciousPlayer(Share share, ABCP<F, TVRFPub, TVRFPriv, TVRFProof> scheme, ISecureRandomNumberGenerator randomMessageGenerator) { 
                this.share = share; 
                this.playerIndexes = scheme.ShareIndices();
                this.randomMessageGenerator = randomMessageGenerator;
                this.scheme = scheme;
            }
            public F Index { get { return share.i; } }
            public F GetRoundExpectedSender(int round) {
                return scheme.field.FromInt((round - 1) % playerIndexes.Count() + 1);
            }
            public IEnumerable<F> GetMessageReceivers() { return randomMessageGenerator == null ? new F[0] { } : playerIndexes; }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) { 
                return randomMessageGenerator == null ? null : scheme.vrfs.RandomMaliciousValue(randomMessageGenerator); 
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
