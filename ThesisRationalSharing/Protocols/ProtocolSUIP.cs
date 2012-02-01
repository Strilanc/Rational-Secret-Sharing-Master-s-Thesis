using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class SUIP<F> where F : IFiniteField<F>, IEquatable<F> {
        [DebuggerDisplay("{ToString()}")]
        public class Share {
            public readonly SplitSignedValue.ForReceiver[][] MessageVerifiers;
            public readonly SplitSignedValue.ForSender[][] SignedMessages;
            public readonly F[] ShortMessage;
            public readonly F i; //share index
            public Share(F i, SplitSignedValue.ForReceiver[][] messageVerifiers, SplitSignedValue.ForSender[][] signedMessages, F[] shortMessage) {
                this.i = i;
                this.MessageVerifiers = messageVerifiers;
                this.SignedMessages = signedMessages;
                this.ShortMessage = shortMessage;
            }
            public override string ToString() {
                return "SUIP Share " + i;
            }
        }

        public readonly int t;
        public readonly int n;
        public readonly F field;
        public readonly Rational alpha;
        public readonly Rational gamma;
        public readonly int omega;
        public readonly int beta;
        public SUIP(int t, int n, F field, Rational alpha, Rational gamma, int omega, int beta) {
            this.t = t;
            this.n = n;
            this.field = field;
            this.alpha = alpha;
            this.gamma = gamma;
            this.omega = omega;
            this.beta = beta;
        }

        private static F[] ShareIndices(int n, F f) {
            var r = new F[n];
            r[0] = f.One;
            for (int i = 1; i < n; i++)
                r[i] = r[i - 1].Plus(f.One);
            return r;
        }
        private BigInteger ChooseDefinitiveRound(ISecureRandomNumberGenerator rng, BigInteger Lc) {
            var r = BigInteger.One + 1;
            while (r <= beta) {
                if (rng.GenerateNextChance(alpha)) return r;
                r += 1;
            }
            while (r <= Lc) {
                if (rng.GenerateNextChance((alpha - gamma) / (1 - gamma))) return r;
                r += 1;
            }
            return r;
        }
        
        private static Dictionary<F, Tuple<Polynomial<F>, Point<F>>> SignMessage(F message, F sender, IEnumerable<F> receivers, ISecureRandomNumberGenerator rng) {
            return receivers.ToDictionary(e => e, e => LinePointCommitment<F>.CreateSignedMessageAndVerifier(message, rng));
        }
        [DebuggerDisplay("{ToString()}")]
        public class SplitSignedValue {
            [DebuggerDisplay("{ToString()}")]
            public class ForSender {
                private readonly Dictionary<F, Polynomial<F>> _state;
                private ForSender(Dictionary<F, Polynomial<F>> _state) {
                    this._state = _state;
                }
                
                public static ForSender From(F sender, SplitSignedValue source) {
                    return new ForSender(source._state[sender].ToDictionary(f => f.Key, f => f.Value.Item1));
                }

                public F Share {
                    get {
                        var p = _state.First().Value;
                        var x = _state.First().Key;
                        return p.EvaluateAt(x.One).Minus(p.EvaluateAt(x.Zero));
                    }
                }
                public Polynomial<F> GetMessageSignatureTo(F receiver) {
                    return _state[receiver];
                }

                public override string ToString() {
                    return "SplitSignedValue.ForSender: Share = " + Share;
                }
            }
            public class ForReceiver {
                private readonly Dictionary<F, Point<F>> _state;
                private ForReceiver(Dictionary<F, Point<F>> _state) {
                    this._state = _state;
                }
                public static ForReceiver From(F receiver, SplitSignedValue source) {
                    return new ForReceiver(source._state.ToDictionary(e => e.Key, e => e.Value[receiver].Item2));
                }
                public Point<F> GetMessageVerifierFrom(F sender) {
                    return _state[sender];
                }
                public override string ToString() {
                    return "SplitSignedValue.ForReceiver";
                }
            }

            private readonly Dictionary<F, Dictionary<F, Tuple<Polynomial<F>, Point<F>>>> _state;
            private SplitSignedValue(Dictionary<F, Dictionary<F, Tuple<Polynomial<F>, Point<F>>>> _state) {
                this._state = _state;
            }
            public static SplitSignedValue FromValue(F value, int t, int n, IEnumerable<F> players, ISecureRandomNumberGenerator rng) {
                var shares = ShamirSecretSharing<F>.CreateShares(value, t, n, rng);
                var shareDic = shares.ToDictionary(e => e.X, e => e.Y);
                var signedMessages = players.ToDictionary(e => e, e => SignMessage(shareDic[e], e, players, rng));
                return new SplitSignedValue(signedMessages);
            }
            public F Value {
                get {
                    return Polynomial<F>.FromInterpolation(_state.Keys.Select(e => new Point<F>(e, ShareYFor(e)))).EvaluateAt(_state.Keys.First().Zero);
                }
            }
            public Point<F> GetMessageVerifierFromTo(F sender, F receiver) {
                return _state[sender][receiver].Item2;
            }
            public F ShareYFor(F sender) {
                return WithOnlySignaturesFor(sender).Share;
            }
            public Polynomial<F> GetMessageSignatureFromTo(F sender, F receiver) {
                return _state[sender][receiver].Item1;
            }
            public ForReceiver WithOnlyVerifiersFor(F receiver) {
                return ForReceiver.From(receiver, this);
            }
            public ForSender WithOnlySignaturesFor(F sender) {
                return ForSender.From(sender, this);
            }
            public static bool Verify(F sender, F receiver, ForSender senderStuff, ForReceiver receiverStuff) {
                return LinePointCommitment<F>.AuthenticateMessageUsingVerifier(
                    senderStuff.GetMessageSignatureTo(receiver),
                    receiverStuff.GetMessageVerifierFrom(sender));
            }
            public override string ToString() {
                return "SplitSignedValue: Value = " + Value;
            }
        }

        public Share[] Deal(F secret, ISecureRandomNumberGenerator rng) {
            var indexes = ShareIndices(n, secret);

            var L = indexes.Zip(indexes.Select(i => rng.GenerateNextValuePoisson(gamma) + 1).PartialSums().Select(i => i + beta + 1).Shuffle(rng), (e1,e2) => Tuple.Create(e1, e2)).ToDictionary(e => e.Item1, e => e.Item2);
            var Ln = L.Values.Max() + 1;
            var c = L.Keys.MinBy(i => L[i]);
            var Lc = L[c];
            var r = ChooseDefinitiveRound(rng, Lc);

            var SI = Enumerable.Range(1, (int)Ln + 1).Select(i =>
                        Enumerable.Range(0, omega + 1).Select(j => {
                            var m = i == r ? (j == 0 ? secret : secret.Zero) : secret.Random(rng);
                            return SplitSignedValue.FromValue(m, t, n, indexes, rng);
                        }).ToArray()
                    ).ToArray();

            var X = Enumerable.Range(0, omega + 1).Select(j => {
                var vn = SI[(int)r - 1][j].ShareYFor(c);
                var vp = SI[(int)r - 2][j].ShareYFor(c);
                return vn.Minus(vp);
            }).ToArray();

            return indexes.Select(i => {
                var sigs = SI.Select(ri => ri.Select(oi => oi.WithOnlySignaturesFor(i)).ToArray()).Take((int)L[i]).ToArray();
                var vers = SI.Select(ri => ri.Select(oi => oi.WithOnlyVerifiersFor(i)).ToArray()).Take((int)L[i] + 1).ToArray();
                return new Share(i, vers, sigs, X);
            }).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < t) throw new ArgumentException("Not enough shares");
            var sr = availableShares.MinBy(e => e.SignedMessages.Length);
            for (var r = 1; r <= sr.SignedMessages.Length + 1; r++) {
                var M = Enumerable.Range(0, omega + 1).Select(oi =>
                            ShamirSecretSharing<F>.CombineShares(t,
                                availableShares.Select(e => {
                                    F y;
                                    if (e == sr && r > e.SignedMessages.Length)
                                        y = e.ShortMessage[oi].Plus(e.SignedMessages.Last()[oi].Share);
                                    else
                                        y = e.SignedMessages[r - 1][oi].Share;
                                    return new Point<F>(e.i, y);
                                }).ToArray())).ToArray();
                if (M.Skip(1).All(e => e.Equals(e.Zero))) return M.First();
            }
            throw new Exception();
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, n, t, omega); }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, n, t, rng, omega); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, n, t, null, omega); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                var messages = players.ToDictionary(e => e.Index, e => Tuple.Create(e.GetRoundMessageAndSignatures(r), e.GetRoundMessageReceivers()));
                var receivedMessages = new Dictionary<F, Dictionary<F, SplitSignedValue.ForSender[]>>();
                foreach (var receiver in players) {
                    var d = new Dictionary<F, SplitSignedValue.ForSender[]>();
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
            SplitSignedValue.ForSender[] GetRoundMessageAndSignatures(int round);
            IEnumerable<F> GetRoundMessageReceivers();
            void UseRoundMessages(int round, Dictionary<F, SplitSignedValue.ForSender[]> messages);
            Tuple<F> RecoveredSecretValue { get; }
            string DoneReason();
        }

        [DebuggerDisplay("{ToString()}")]
        public class RationalPlayer : IPlayer {
            public readonly Share share;
            public readonly HashSet<F> cooperatorIndexes = new HashSet<F>();
            public readonly int n;
            public readonly int t;
            public readonly int omega;
            private Tuple<F> secret = null;
            private Dictionary<F, List<Point<F>>> lastMessages = null;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue { get { return secret; } }

            public RationalPlayer(Share share, int n, int t, int omega) {
                this.share = share;
                this.n = n;
                this.t = t;
                this.omega = omega;
                foreach (var fi in ShareIndices(n, share.i))
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (secret != null) return "Have secret";
                if (cooperatorIndexes.Count < t) return "Not enough cooperators";
                return null;
            }
            public SplitSignedValue.ForSender[] GetRoundMessageAndSignatures(int round) {
                if (secret != null) return null;
                if (cooperatorIndexes.Count < t) return null;
                if (round > share.SignedMessages.Length) return null;
                return share.SignedMessages[round - 1];
            }
            public IEnumerable<F> GetRoundMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessages(int round, Dictionary<F, SplitSignedValue.ForSender[]> messages) {
                if (secret != null) return;
                if (cooperatorIndexes.Count < t) return;

                var lastCoops = new HashSet<F>(cooperatorIndexes);
                var receivedMessages = new Dictionary<F, List<Point<F>>>();
                foreach (var m in messages) {
                    if (!cooperatorIndexes.Contains(m.Key)) continue;
                    if (!Enumerable.Range(0, omega + 1).All(oi => SplitSignedValue.Verify(m.Key, share.i, m.Value[oi], share.MessageVerifiers[round - 1][oi]))) {
                        cooperatorIndexes.Remove(m.Key);
                        continue;
                    }
                    receivedMessages.Add(m.Key, Enumerable.Range(0, omega + 1).Select(oi => new Point<F>(m.Key, m.Value[oi].Share)).ToList());
                }
                cooperatorIndexes.IntersectWith(messages.Keys);

                lastCoops.ExceptWith(cooperatorIndexes);
                var extraShares = new List<Dictionary<F, List<Point<F>>>>();
                if (round > 1 && cooperatorIndexes.Count == t - 1) {
                    foreach (var c in lastCoops) {
                        extraShares.Add(new Dictionary<F, List<Point<F>>> {
                            {c, lastMessages[c].Zip(share.ShortMessage, (p, n) => new Point<F>(c, p.Y.Plus(n))).ToList()}
                        });
                    }
                } else if (cooperatorIndexes.Count >= t) {
                    extraShares.Add(new Dictionary<F, List<Point<F>>>());
                }
                lastMessages = receivedMessages;

                if (cooperatorIndexes.Count < t - 1) return;
                foreach (var ex in extraShares) {
                    var c = receivedMessages.Concat(ex).ToArray();
                    var s = Enumerable.Range(0, c.First().Value.Count()).Select(i => ShamirSecretSharing<F>.CombineShares(t, c.Select(e => e.Value[i]).ToArray())).ToArray();
                    if (s.Skip(1).All(e => e.Equals(e.Zero))) {
                        secret = Tuple.Create(s.First());
                        return;
                    }
                }
            }
            public override string ToString() { return "SUIP Rational Player " + share.i; }
        }
        [DebuggerDisplay("{ToString()}")]
        public class MaliciousPlayer : IPlayer {
            public readonly Share share;
            public readonly ISecureRandomNumberGenerator randomMessageGenerator;
            public readonly int omega;
            public readonly IEnumerable<F> playerIndexes;
            public readonly int t;
            public MaliciousPlayer(Share share, int n, int t, ISecureRandomNumberGenerator randomMessageGenerator, int omega) { 
                this.share = share; 
                this.playerIndexes = ShareIndices(n, share.i);
                this.randomMessageGenerator = randomMessageGenerator;
                this.omega = omega;
                this.t = t;
            }
            public F Index { get { return share.i; } }
            public IEnumerable<F> GetRoundMessageReceivers() { return randomMessageGenerator == null ? new F[0] { } : playerIndexes; }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public SplitSignedValue.ForSender[] GetRoundMessageAndSignatures(int round) {
                if (randomMessageGenerator == null) return null;
                return Enumerable.Range(0, omega + 1).Select(oi => {
                    var m = share.i.Random(randomMessageGenerator);
                    return SplitSignedValue.FromValue(m, t, playerIndexes.Count(), playerIndexes, randomMessageGenerator).WithOnlySignaturesFor(share.i);
                }).ToArray();
            }
            public void UseRoundMessages(int round, Dictionary<F, SplitSignedValue.ForSender[]> messages) { }
            public override string ToString() { return "SUIP Malicious Player " + share.i; }
        }

        public override string ToString() {
            return String.Format("SBP: n={0}, t={1}", n, t);
        }
    }
}
