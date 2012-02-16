using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace ThesisRationalSharing.Protocols {
    [DebuggerDisplay("{ToString()}")]
    public class SBP<F, TVRFPub, TVRFPriv, TVRFProof> {
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

        public readonly int ThresholdShareCount;
        public readonly int TotalShareCount;
        public readonly IFiniteField<F> Field;
        public readonly ICommitmentScheme<F> CommitmentScheme;
        public readonly IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> VerifiableRandomFunctionScheme;
        public readonly Rational MarginalDefinitiveRoundProbability;
        public SBP(int thresholdShareCount, 
                   int totalShareCount, 
                   IFiniteField<F> field, 
                   ICommitmentScheme<F> commitmentScheme, 
                   IVerifiableRandomFunctionScheme<TVRFPub, TVRFPriv, TVRFProof, F> verifiableRandomFunctionScheme, 
                   Rational marginalDefinitiveRoundProbability) {
            Contract.Requires(field != null);
            Contract.Requires(commitmentScheme != null);
            Contract.Requires(verifiableRandomFunctionScheme != null);
            Contract.Requires(thresholdShareCount >= 2);
            Contract.Requires(totalShareCount >= thresholdShareCount);
            Contract.Requires(totalShareCount < field.Size);
            Contract.Requires(marginalDefinitiveRoundProbability > 0);
            Contract.Requires(marginalDefinitiveRoundProbability < 1);
            this.ThresholdShareCount = thresholdShareCount;
            this.TotalShareCount = totalShareCount;
            this.Field = field;
            this.CommitmentScheme = commitmentScheme;
            this.VerifiableRandomFunctionScheme = verifiableRandomFunctionScheme;
            this.MarginalDefinitiveRoundProbability = marginalDefinitiveRoundProbability;
        }

        private F[] ShareIndexes() {
            return Field.Range(Field.One, TotalShareCount).ToArray();
        }
        public Share[] Deal(F secret, ISecureRandomNumberGenerator rng) {
            var indexes = ShareIndexes();

            //Commit to the secret 
            var c = CommitmentScheme.Create(secret, rng);
            //Create public (V) and private (G) key pairs
            var vg = indexes.MapTo(i => VerifiableRandomFunctionScheme.CreatePublicPrivateKeyPair(rng));
            var V = indexes.MapTo(i => vg[i].Item1);
            var G = indexes.MapTo(i => vg[i].Item2);
            //Choose the random definitive round
            var r = rng.GenerateNextValueGeometric(chanceStop: MarginalDefinitiveRoundProbability, min: 1);
            //Create shamir shares for the secret
            var S = ShamirSecretSharing.CreateShares(Field, secret, ThresholdShareCount, TotalShareCount, rng).KeyBy(e => e.X);
            //Compute the offsets between each player's share and vrf output on the definitive round
            var Y = indexes.MapTo(i => Field.Subtract(S[i].Y, VerifiableRandomFunctionScheme.Generate(G[i], r).Value));
            
            //Return the list of shares composed of an index, a private key, the commitment, all public keys, and all offsets
            return indexes.Select(i => new Share(i, c, V, Y, G[i])).ToArray();
        }

        /// <summary>
        /// Combines a group of shares as if the players holding the shares were not adversarial.
        /// </summary>
        public F CoalitionCombine(Share[] availableShares) {
            if (availableShares.Length < ThresholdShareCount) throw new ArgumentException("Not enough shares");
            var r = 1;
            var c = availableShares.FirstOrDefault().c;
            while (true) {
                var M = availableShares.Select(e => VerifiableRandomFunctionScheme.Generate(e.G, r)).ToArray();
                var S = availableShares.Zip(M, (e, m) => new Point<F>(Field, e.i, Field.Add(m.Value, e.Y[e.i]))).ToArray();
                var s = ShamirSecretSharing.TryCombineShares(Field, ThresholdShareCount, S);
                if (s != null && c.Matches(s.Item1))
                    return s.Item1;
                r += 1;
            }
        }

        public IPlayer MakeCooperateUntilLearnPlayer(Share share) { return new RationalPlayer(share, this); }
        public IPlayer MakeSendRandomMessagesPlayer(Share share, ISecureRandomNumberGenerator rng) { return new MaliciousPlayer(share, this, rng); }
        public IPlayer MakeSendNoMessagePlayer(Share share) { return new MaliciousPlayer(share, this, null); }

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
            public readonly SBP<F, TVRFPub, TVRFPriv, TVRFProof> Scheme;
            public readonly Share Share;
            private readonly ISet<F> cooperatorIndexes;
            public Tuple<F> RecoveredSecretValue { get; private set; }
            public F Index { get { return Share.i; } }

            public RationalPlayer(Share share, SBP<F, TVRFPub, TVRFPriv, TVRFProof> scheme) {
                this.Share = share;
                this.Scheme = scheme;
                this.cooperatorIndexes = new HashSet<F>(scheme.ShareIndexes());
            }

            public string DoneReason() {
                if (RecoveredSecretValue != null) return "Have secret";
                if (cooperatorIndexes.Count < Scheme.ThresholdShareCount) return "Not enough cooperators";
                return null;
            }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (RecoveredSecretValue != null) return null;
                if (cooperatorIndexes.Count < Scheme.ThresholdShareCount) return null;
                return Scheme.VerifiableRandomFunctionScheme.Generate(Share.G, round);
            }
            public IEnumerable<F> GetRoundMessageReceivers() {
                return cooperatorIndexes;
            }
            public void UseRoundMessages(int round, Dictionary<F, ProofValue<TVRFProof, F>> messages) {
                if (RecoveredSecretValue != null) return;

                //ignore players who sent no message
                cooperatorIndexes.IntersectWith(messages.Keys);
                //ignore players who sent fake messages
                cooperatorIndexes.IntersectWith(cooperatorIndexes.Where(j => Scheme.VerifiableRandomFunctionScheme.Verify(Share.V[j], round, messages[j])));

                //abort when there are not enough cooperators to reconstruct the secret
                if (cooperatorIndexes.Count < Scheme.ThresholdShareCount) return;

                var shares = cooperatorIndexes.Select(j => Scheme.Field.Point(j, Scheme.Field.Add(messages[j].Value, Share.Y[j]))).ToArray();
                var potentialSecret = ShamirSecretSharing.TryCombineShares(Scheme.Field, Scheme.ThresholdShareCount, shares);
                if (potentialSecret != null && Share.c.Matches(potentialSecret.Item1)) RecoveredSecretValue = potentialSecret;
            }
            public override string ToString() { return "SBP Rational Player " + Share.i; }
        }
        [DebuggerDisplay("{ToString()}")]
        public class MaliciousPlayer : IPlayer {
            public readonly Share Share;
            public readonly SBP<F, TVRFPub, TVRFPriv, TVRFProof> Scheme;
            private readonly ISecureRandomNumberGenerator _rng;
            public MaliciousPlayer(Share share, SBP<F, TVRFPub, TVRFPriv, TVRFProof> scheme, ISecureRandomNumberGenerator randomMessageGenerator = null) { 
                this.Share = share; 
                this.Scheme = scheme;
                this._rng = randomMessageGenerator;
            }
            public F Index { get { return Share.i; } }
            public IEnumerable<F> GetRoundMessageReceivers() { return Scheme.ShareIndexes(); }
            public Tuple<F> RecoveredSecretValue { get { return null; } }
            public string DoneReason() { return "Malicious"; }
            public ProofValue<TVRFProof, F> GetRoundMessage(int round) {
                if (_rng == null) return null;
                return Scheme.VerifiableRandomFunctionScheme.RandomMaliciousValue(_rng); 
            }
            public void UseRoundMessages(int round, Dictionary<F, ProofValue<TVRFProof, F>> messages) { }
            public override string ToString() { return "SBP Malicious Player " + Share.i; }
        }

        public override string ToString() {
            return String.Format("SBP: n={0}, t={1}, alpha={2}, field={3}, CS={4}, VRFS={5}", 
                TotalShareCount, 
                ThresholdShareCount, 
                MarginalDefinitiveRoundProbability,
                Field,
                CommitmentScheme,
                VerifiableRandomFunctionScheme);
        }
    }
}
