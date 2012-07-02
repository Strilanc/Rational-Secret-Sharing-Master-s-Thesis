using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class SUIP<F> {
        ///<summary>A SUIP share of a secret.</summary>
        [DebuggerDisplay("{ToString()}")]
        public class Share {
            public readonly AuthenticatedMessageData.ForReceiver[][] MessageVerifiers;
            public readonly AuthenticatedMessageData.ForSender[][] SignedMessages;
            public readonly F[] ShortMessage;
            public readonly F i; //share index
            public Share(F i, AuthenticatedMessageData.ForReceiver[][] messageVerifiers, AuthenticatedMessageData.ForSender[][] signedMessages, F[] shortMessage) {
                this.i = i;
                this.MessageVerifiers = messageVerifiers;
                this.SignedMessages = signedMessages;
                this.ShortMessage = shortMessage;
            }
            public override string ToString() {
                return "SUIP Share " + i;
            }
        }

        ///<summary>Threshold number of shares required to reconstruct a secret.</summary>
        public readonly int ThresholdShareCount;
        ///<summary>Total number of shares created for a secret.</summary>
        public readonly int TotalShareCount;
        ///<summary>Finite field secrets are chosen from.</summary>
        public readonly IFiniteField<F> Field;
        ///<summary>Marginal probability of the definitive round occuring per round.</summary>
        public readonly Rational alpha;
        ///<summary>Marginal probability of a list ending per round.</summary>
        public readonly Rational gamma;
        ///<summary>Number of indicators.</summary>
        public readonly int omega;
        ///<summary>Minimum list size.</summary>
        public readonly int beta;

        public SUIP(int t, int n, IFiniteField<F> field, Rational alpha, Rational gamma, int omega, int beta) {
            this.ThresholdShareCount = t;
            this.TotalShareCount = n;
            this.Field = field;
            this.alpha = alpha;
            this.gamma = gamma;
            this.omega = omega;
            this.beta = beta;
        }

        private F[] ShareIndices() {
            var r = new F[TotalShareCount];
            r[0] = Field.One;
            for (int i = 1; i < TotalShareCount; i++)
                r[i] = Field.Add(r[i - 1], Field.One);
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
        
        private Dictionary<F, Tuple<Polynomial<F>, Point<F>>> SignMessage(F message, F sender, IEnumerable<F> receivers, ISecureRandomNumberGenerator rng) {
            return receivers.ToDictionary(e => e, e => LinePointCommitment.CreateSignedMessageAndVerifier(Field, message, rng));
        }

        ///<summary>The data used to sign and verify the messages for a value split into shares.</summary>
        [DebuggerDisplay("{ToString()}")]
        public class AuthenticatedMessageData {
            ///<summary>The data used to sign the messages for one of the shares of a value split into shares.</summary>
            [DebuggerDisplay("{ToString()}")]
            public class ForSender {
                private readonly IField<F> Field;
                private readonly Dictionary<F, Polynomial<F>> _signatures;
                private ForSender(IField<F> field, Dictionary<F, Polynomial<F>> signatures) {
                    this._signatures = signatures;
                    this.Field = field;
                }
                
                public static ForSender From(IField<F> field, F sender, AuthenticatedMessageData source) {
                    return new ForSender(field, source._state[sender].ToDictionary(f => f.Key, f => f.Value.Item1));
                }

                public F Share {
                    get {
                        var p = _signatures.First().Value;
                        return Field.Subtract(p.EvaluateAt(Field.One), p.EvaluateAt(Field.Zero));
                    }
                }
                public Polynomial<F> GetMessageSignatureTo(F receiver) {
                    return _signatures[receiver];
                }

                public override string ToString() {
                    return "SplitSignedValue.ForSender: Share = " + Share;
                }
            }
            ///<summary>The data used to verify the messages for one of the shares of a value split into shares.</summary>
            [DebuggerDisplay("{ToString()}")]
            public class ForReceiver {
                private readonly Dictionary<F, Point<F>> _state;
                private ForReceiver(Dictionary<F, Point<F>> _state) {
                    this._state = _state;
                }
                public static ForReceiver From(F receiver, AuthenticatedMessageData source) {
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
            private readonly IField<F> Field;
            private AuthenticatedMessageData(IField<F> field, Dictionary<F, Dictionary<F, Tuple<Polynomial<F>, Point<F>>>> _state) {
                this._state = _state;
                this.Field = field;
            }
            public static AuthenticatedMessageData FromValue(SUIP<F> scheme, F value, ISecureRandomNumberGenerator rng) {
                var shares = ShamirSecretSharing.CreateShares(scheme.Field, value, scheme.ThresholdShareCount, scheme.TotalShareCount, rng);
                var shareDic = shares.ToDictionary(e => e.X, e => e.Y);
                var signedMessages = scheme.ShareIndices().ToDictionary(e => e, e => scheme.SignMessage(shareDic[e], e, scheme.ShareIndices(), rng));
                return new AuthenticatedMessageData(scheme.Field, signedMessages);
            }
            public F Value {
                get {
                    return Polynomial<F>.FromInterpolation(Field, _state.Keys.Select(e => new Point<F>(Field, e, ShareYFor(e)))).EvaluateAt(Field.Zero);
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
                return ForSender.From(Field, sender, this);
            }
            public static bool Verify(F sender, F receiver, ForSender senderStuff, ForReceiver receiverStuff) {
                return LinePointCommitment.AuthenticateMessageUsingVerifier(
                    senderStuff.GetMessageSignatureTo(receiver),
                    receiverStuff.GetMessageVerifierFrom(sender));
            }
            public override string ToString() {
                return "SplitSignedValue: Value = " + Value;
            }
        }

        public Share[] Deal(F secret, ISecureRandomNumberGenerator rng) {
            var indexes = ShareIndices();

            var L = indexes.Zip(indexes.Select(i => rng.GenerateNextValueGeometric(chanceStop: gamma, min: 1))
                                       .PartialSums()
                                       .Select(i => i + beta + 1)
                                       .Shuffle(rng), 
                                (e1,e2) => Tuple.Create(e1, e2)).ToDictionary(e => e.Item1, e => e.Item2);
            var Ln = L.Values.Max() + 1;
            var c = L.Keys.MinBy(i => L[i]);
            var Lc = L[c];
            var r = ChooseDefinitiveRound(rng, Lc);

            var SI = Enumerable.Range(1, (int)Ln + 1).Select(i =>
                         Enumerable.Range(0, omega + 1).Select(j => {
                             var m = i == r ? (j == 0 ? secret : Field.Zero) : Field.Random(rng);
                             return AuthenticatedMessageData.FromValue(this, m, rng);
                         }).ToArray()
                     ).ToArray();

            var X = Enumerable.Range(0, omega + 1).Select(j => {
                var vn = SI[(int)r - 1][j].ShareYFor(c);
                var vp = SI[(int)r - 2][j].ShareYFor(c);
                return Field.Subtract(vn, vp);
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
            if (availableShares.Length < ThresholdShareCount) throw new ArgumentException("Not enough shares");
            var sr = availableShares.MinBy(e => e.SignedMessages.Length);
            for (var r = 1; r <= sr.SignedMessages.Length + 1; r++) {
                var M = Enumerable.Range(0, omega + 1).Select(oi =>
                            ShamirSecretSharing.CombineShares(Field, ThresholdShareCount,
                                availableShares.Select(e => {
                                    F y;
                                    if (e == sr && r > e.SignedMessages.Length)
                                        y = Field.Add(e.ShortMessage[oi], e.SignedMessages.Last()[oi].Share);
                                    else
                                        y = e.SignedMessages[r - 1][oi].Share;
                                    return new Point<F>(Field, e.i, y);
                                }).ToArray())).ToArray();
                if (M.Skip(1).All(e => Field.IsZero(e))) return M.First();
            }
            throw new Exception();
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, this); }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, this, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, this, null); }

        public void RunProtocol(IEnumerable<IPlayer> players) {
            var r = 1;
            while (players.Any(e => e.DoneReason() == null)) {
                var messages = players.ToDictionary(e => e.Index, e => Tuple.Create(e.GetRoundMessageAndSignatures(r), e.GetRoundMessageReceivers()));
                var receivedMessages = new Dictionary<F, Dictionary<F, AuthenticatedMessageData.ForSender[]>>();
                foreach (var receiver in players) {
                    var d = new Dictionary<F, AuthenticatedMessageData.ForSender[]>();
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
            AuthenticatedMessageData.ForSender[] GetRoundMessageAndSignatures(int round);
            IEnumerable<F> GetRoundMessageReceivers();
            void UseRoundMessages(int round, Dictionary<F, AuthenticatedMessageData.ForSender[]> messages);
            Tuple<F> RecoveredSecretValue { get; }
            string DoneReason();
        }

        [DebuggerDisplay("{ToString()}")]
        public class RationalPlayer : IPlayer {
            public readonly Share share;
            public readonly HashSet<F> cooperatorIndexes = new HashSet<F>();
            public readonly SUIP<F> scheme;
            private Tuple<F> secret = null;
            private Dictionary<F, List<Point<F>>> lastMessages = null;
            public F Index { get { return share.i; } }
            public Tuple<F> RecoveredSecretValue { get { return secret; } }

            public RationalPlayer(Share share, SUIP<F> scheme) {
                this.share = share;
                this.scheme = scheme;
                foreach (var fi in scheme.ShareIndices())
                    cooperatorIndexes.Add(fi);
            }

            public string DoneReason() {
                if (secret != null) return "Have secret";
                if (cooperatorIndexes.Count < scheme.ThresholdShareCount) return "Not enough cooperators";
                return null;
            }
            public AuthenticatedMessageData.ForSender[] GetRoundMessageAndSignatures(int round) {
                if (secret != null) return null;
                if (cooperatorIndexes.Count < scheme.ThresholdShareCount) return null;
                if (round > share.SignedMessages.Length) return null;
                return share.SignedMessages[round - 1];
            }
            public IEnumerable<F> GetRoundMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessages(int round, Dictionary<F, AuthenticatedMessageData.ForSender[]> messages) {
                if (secret != null) return;
                if (cooperatorIndexes.Count < scheme.ThresholdShareCount) return;

                var lastCoops = new HashSet<F>(cooperatorIndexes);
                var receivedMessages = new Dictionary<F, List<Point<F>>>();
                foreach (var m in messages) {
                    if (!cooperatorIndexes.Contains(m.Key)) continue;
                    if (!Enumerable.Range(0, scheme.omega + 1).All(oi => AuthenticatedMessageData.Verify(m.Key, share.i, m.Value[oi], share.MessageVerifiers[round - 1][oi]))) {
                        cooperatorIndexes.Remove(m.Key);
                        continue;
                    }
                    receivedMessages.Add(m.Key, Enumerable.Range(0, scheme.omega + 1).Select(oi => new Point<F>(scheme.Field, m.Key, m.Value[oi].Share)).ToList());
                }
                cooperatorIndexes.IntersectWith(messages.Keys);

                lastCoops.ExceptWith(cooperatorIndexes);
                var extraShares = new List<Dictionary<F, List<Point<F>>>>();
                if (round > 1 && cooperatorIndexes.Count == scheme.ThresholdShareCount - 1) {
                    foreach (var c in lastCoops) {
                        extraShares.Add(new Dictionary<F, List<Point<F>>> {
                            {c, lastMessages[c].Zip(share.ShortMessage, (p, n) => new Point<F>(scheme.Field, c, scheme.Field.Add(p.Y, n))).ToList()}
                        });
                    }
                } else if (cooperatorIndexes.Count >= scheme.ThresholdShareCount) {
                    extraShares.Add(new Dictionary<F, List<Point<F>>>());
                }
                lastMessages = receivedMessages;

                if (cooperatorIndexes.Count < scheme.ThresholdShareCount - 1) return;
                foreach (var ex in extraShares) {
                    var c = receivedMessages.Concat(ex).ToArray();
                    var s = Enumerable.Range(0, c.First().Value.Count()).Select(i => ShamirSecretSharing.CombineShares(scheme.Field, scheme.ThresholdShareCount, c.Select(e => e.Value[i]).ToArray())).ToArray();
                    if (s.Skip(1).All(e => scheme.Field.IsZero(e))) {
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
            public readonly SUIP<F> scheme;
            public readonly IEnumerable<F> playerIndexes;
            public MaliciousPlayer(Share share, SUIP<F> scheme, ISecureRandomNumberGenerator randomMessageGenerator) { 
                this.share = share; 
                this.playerIndexes = scheme.ShareIndices();
                this.randomMessageGenerator = randomMessageGenerator;
                this.scheme = scheme;
            }
            public F Index { get { return share.i; } }
            public IEnumerable<F> GetRoundMessageReceivers() { return randomMessageGenerator == null ? new F[0] { } : playerIndexes; }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public AuthenticatedMessageData.ForSender[] GetRoundMessageAndSignatures(int round) {
                if (randomMessageGenerator == null) return null;
                return Enumerable.Range(0, scheme.omega + 1).Select(oi => {
                    var m = scheme.Field.Random(randomMessageGenerator);
                    return AuthenticatedMessageData.FromValue(scheme, m, randomMessageGenerator).WithOnlySignaturesFor(share.i);
                }).ToArray();
            }
            public void UseRoundMessages(int round, Dictionary<F, AuthenticatedMessageData.ForSender[]> messages) { }
            public override string ToString() { return "SUIP Malicious Player " + share.i; }
        }

        public override string ToString() {
            return String.Format("SBP: n={0}, t={1}", TotalShareCount, ThresholdShareCount);
        }
    }
}
