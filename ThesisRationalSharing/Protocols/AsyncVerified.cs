using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class AsyncVerifiedProtocol {
    public static AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> From<TProof, TPublicKey, TPrivateKey>(
            ShamirSecretSharing wrappedSharingScheme,
            IVerifiableRandomFunctionScheme<TPublicKey, TPrivateKey, TProof> vrfScheme) {
        Contract.Requires(wrappedSharingScheme != null);
        Contract.Requires(vrfScheme != null);
        return new AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey>(wrappedSharingScheme, vrfScheme);
    }
}
public class AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> : ISecretSharingScheme<AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey>.Share> {
    public readonly ShamirSecretSharing wrappedSharingScheme;
    public readonly IVerifiableRandomFunctionScheme<TPublicKey, TPrivateKey, TProof> vrfScheme;
    public BigInteger Range { get { return wrappedSharingScheme.Range; } }

    /// <param name="wrappedSharingScheme">Underlying augmented sharing scheme.</param>
    /// <param name="publicCryptoScheme">Encrypts deterministic base round messages.</param>
    /// <param name="shareMixingScheme">Used to reversibly mask true shares using round messages.</param>
    /// <param name="roundNonceMixingScheme">Scrambles the round using a nonce. Output must match input for publicCryptoScheme encryption.</param>
    public AsyncVerifiedProtocol(
            ShamirSecretSharing wrappedSharingScheme,
            IVerifiableRandomFunctionScheme<TPublicKey, TPrivateKey, TProof> vrfScheme) {
        Contract.Requires(wrappedSharingScheme != null);
        Contract.Requires(vrfScheme != null);
        this.wrappedSharingScheme = wrappedSharingScheme;
        this.vrfScheme = vrfScheme;
    }

    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        var targetRound = rng.GenerateNextValueMod(total) + rng.GenerateNextValuePoisson(new Rational(5, 6));
        return Create(secret, threshold, total, rng, targetRound);
    }
    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng, BigInteger targetRound) {
        Contract.Requires(targetRound >= 0);
        var nonce = rng.GenerateNextValueMod(BigInteger.One << 128);
        var keys = Enumerable.Range(0, total).Select(e => vrfScheme.CreatePublicPrivateKeyPair(rng)).ToArray();
        var common = new CommonShare(
            nonce: nonce, 
            publicKeys: keys.Select(e => e.Item1).ToArray(), 
            commitment: CommitSHA1.FromValue(secret),
            threshold: threshold,
            total: total);

        var firstLearnerIndex = (targetRound + threshold - 2) % total;
        return keys.Select((k, i) => {
            var firstUnmaskingRound = (int)(targetRound + (i - firstLearnerIndex).ProperMod(total));
            var wrappedShares = wrappedSharingScheme.Create(secret, threshold, total, rng);
            var shareMasks = from round in Enumerable.Range(firstUnmaskingRound, total)
                             let senderId = round % total
                             let senderKey = keys[senderId].Item2
                             let encryptedMessage = vrfScheme.Generate(senderKey, round, Range)
                             select wrappedShares[senderId].Y - encryptedMessage.Value;
            var alignedShareMasks = shareMasks.Rotate(firstUnmaskingRound).ToArray();
            return new Share(k.Item2, alignedShareMasks, common, i);
        }).ToArray();
    }

    public ProofValue<TProof> GetRoundMessage(BigInteger round, Share share) {
        return vrfScheme.Generate(share.PrivateKey, round, Range);
    }
    public bool IsMessageValid(BigInteger round, BigInteger nonce, TPublicKey key, ProofValue<TProof> message) {
        return vrfScheme.Verify(key, round, Range, message);
    }

    [DebuggerDisplay("{ToString()}")]
    public class CommonShare {
        public readonly BigInteger Nonce;
        public readonly IList<TPublicKey> PublicKeys;
        public readonly ICommitment Commitment;
        public readonly int Threshold;
        public readonly int Total;
        public CommonShare(BigInteger nonce, IList<TPublicKey> publicKeys, ICommitment commitment, int threshold, int total) {
            this.Nonce = nonce;
            this.PublicKeys = publicKeys;
            this.Commitment = commitment;
            this.Threshold = threshold;
            this.Total = total;
        }
        public override string ToString() {
            return String.Format("Nonce: {0}, Threshold: {1}, Total: {2}, Commitment: {3}", Nonce, Threshold, Total, Commitment);
        }
    }
    [DebuggerDisplay("{ToString()}")]
    public class Share {
        public TPublicKey PublicKey { get { return Common.PublicKeys[CommonIndex]; } }
        public readonly IList<ModInt> Offsets;
        public readonly TPrivateKey PrivateKey;
        public readonly CommonShare Common;
        public readonly int CommonIndex;
        public Share(TPrivateKey privateKey, IList<ModInt> offsets, CommonShare common, int commonIndex) {
            this.PrivateKey = privateKey;
            this.Common = common;
            this.CommonIndex = commonIndex;
            this.Offsets = offsets;
        }
        public override string ToString() {
            return String.Format("Id: {0}, PrivateKey: {1}, PublicKey: {2}", CommonIndex, PrivateKey, PublicKey);
        }
    }

    public BigInteger Combine(int degree, IList<Share> shares) {
        if (shares.GroupBy(e => e.Common).Count() > 1) throw new ArgumentException("Unrelated shares.");
        if (degree != shares.First().Common.Threshold) throw new ArgumentException("Shares of different degree.");

        var shareMap = shares.ToDictionary(e => e.CommonIndex, e => e);
        var usedShare = shares.First();
        var total = usedShare.Common.Total;
        var wrappedShareQueue = new Queue<ModPoint>();

        int round = 0;
        while (true) {
            var i = round % total;
            if (!shareMap.ContainsKey(i)) {
                round += 1;
                continue;
            }
            
            var msg = GetRoundMessage(round, shareMap[i]);
            var msk = usedShare.Offsets[i];
            var shr = ModPoint.From(i, msk + msg.Value);
            wrappedShareQueue.Enqueue(shr);
            
            if (wrappedShareQueue.Count > degree) wrappedShareQueue.Dequeue();
            var secret = wrappedSharingScheme.TryCombine(degree, wrappedShareQueue.ToArray());
            if (secret.HasValue && usedShare.Common.Commitment.Matches(secret.Value)) return secret.Value;

            round += 1;
        }
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        return Combine(degree, shares);
    }

    public RationalCoalition MakeRationalCoalition(IEnumerable<Share> shares) {
        Contract.Requires(shares != null);
        return RationalCoalition.FromShares(this, shares.ToArray());
    }
    public RationalPlayer MakeRationalPlayer(Share share) {
        Contract.Requires(share != null);
        return new RationalPlayer(new HonestPlayer(this, share));
    }
    public HonestPlayer MakeHonestPlayer(Share share) {
        Contract.Requires(share != null);
        return new HonestPlayer(this, share);
    }

    [DebuggerDisplay("{ToString()}")]
    public class RationalCoalition {
        public readonly AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> Scheme;
        private readonly Colluder[] Colluders;
        private EndRoundResult result = new EndRoundResult();
        
        public IEnumerable<Share> Shares() {
            return Colluders.Select(e => e.HonestUnderling.Brain.Share);
        }
        public IEnumerable<IActorPlayer<ProofValue<TProof>>> GetPlayers() {
            return Colluders;
        }

        [DebuggerDisplay("{ToString()}")]
        private class Colluder : IActorPlayer<ProofValue<TProof>> {
            public readonly HonestPlayer HonestUnderling;
            public readonly RationalCoalition Coalition;
            public readonly bool IsSilent;

            public Colluder(HonestPlayer honestUnderling, RationalCoalition coalition, bool IsSilent) {
                Contract.Requires(honestUnderling != null);
                Contract.Requires(coalition != null);
                this.HonestUnderling = honestUnderling;
                this.Coalition = coalition;
                this.IsSilent = IsSilent;
            }

            public int Index { get { return HonestUnderling.Index; } }
            public void Init(IEnumerable<IPlayer> players) {
                HonestUnderling.Init(players);
            }
            public Dictionary<IPlayer, ProofValue<TProof>> StartRound(int round) {
                Coalition.TrySneakRound(round + HonestUnderling.Brain.Share.Common.Total);
                if (Coalition.result.OptionalResult.HasValue || IsSilent) return null;
                return HonestUnderling.StartRound(round);
            }
            public EndRoundResult EndRound(int round, Dictionary<IPlayer, ProofValue<TProof>> receivedMessages) {
                var silentSender = Coalition.Colluders.FirstOrDefault(e => e.HonestUnderling.Brain.HaveRoundMessage(round));
                if (silentSender != null) {
                    receivedMessages = new Dictionary<IPlayer, ProofValue<TProof>>() { { silentSender, silentSender.HonestUnderling.Brain.GetRoundMessage(round) } };
                }

                if (!IsSilent) {
                    foreach (var colluder in Coalition.Colluders) {
                        Coalition.result = colluder.HonestUnderling.EndRound(round, receivedMessages);
                        if (Coalition.result.OptionalResult.HasValue) break;
                    }
                }

                return Coalition.result;
            }

            public override string ToString() {
                return String.Format("Colluder (ID: {0}, Silent: {1}) from {2}", Index, IsSilent, Coalition);
            }
       }

        private RationalCoalition(AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> scheme, Share[] shares) {
            Contract.Requires(scheme != null);
            Contract.Requires(shares != null);
            Contract.Requires(shares.Length >= 1);
            Contract.Requires(shares.Select(e => e.Common).Distinct().IsSingle());
            Contract.Requires(shares.Select(e => e.CommonIndex).Duplicates().None());
            this.Colluders = shares.Select((e, i) => new Colluder(new HonestPlayer(scheme, e), this, i == 0)).ToArray();
            this.Scheme = scheme;
            var c = shares.First().Common;
            if (shares.Length >= c.Threshold) {
                this.result = new EndRoundResult(scheme.Combine(c.Threshold, shares));
            }
        }
        public static RationalCoalition FromShares(AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> scheme, Share[] shares) {
            Contract.Requires(scheme != null);
            Contract.Requires(shares != null);
            Contract.Requires(shares.Length >= 1);
            Contract.Requires(shares.Select(e => e.Common).Distinct().IsSingle());
            Contract.Requires(shares.Select(e => e.CommonIndex).Duplicates().None());
            return new RationalCoalition(scheme, shares);
        }

        private int nextSnuckRound = 0;
        private void TrySneakRound(int round) {
            if (result.IsTerminal) return;

            while (nextSnuckRound < round) {
                var p = Colluders.FirstOrDefault(e => e.Index == nextSnuckRound % Shares().First().Common.Total);
                if (p != null) {
                    var msg = p.HonestUnderling.Brain.GetRoundMessage(nextSnuckRound);
                    foreach (var r in Colluders) {
                        r.HonestUnderling.Brain.Receive(nextSnuckRound, p.Index, msg);
                        if (r.HonestUnderling.Brain.Secret.HasValue) {
                            result = new EndRoundResult(r.HonestUnderling.Brain.Secret);
                            return;
                        }
                    }
                }
                nextSnuckRound += 1;
            }
        }

        public override string ToString() {
            return "Rational Coalition: {" + String.Join(", ", Colluders.Select(e => e.Index)) + "}";
        }
    }
    [DebuggerDisplay("{ToString()}")]
    public class RationalPlayer : IActorPlayer<ProofValue<TProof>> {
        public readonly HonestPlayer HonestUnderling;

        public RationalPlayer(HonestPlayer honestUnderling) {
            Contract.Requires(honestUnderling != null);
            this.HonestUnderling = honestUnderling;
        }

        public int Index { get { return HonestUnderling.Index; } }
        public void Init(IEnumerable<IPlayer> players) {
            HonestUnderling.Init(players);
        }
        public Dictionary<IPlayer, ProofValue<TProof>> StartRound(int round) {
            if (HonestUnderling.Secret.HasValue) return null;
 	        return HonestUnderling.StartRound(round);
        }
        public EndRoundResult EndRound(int round, Dictionary<IPlayer, ProofValue<TProof>> receivedMessages) {
            if (HonestUnderling.Secret.HasValue) return new EndRoundResult(HonestUnderling.Secret);
            return HonestUnderling.EndRound(round, receivedMessages);
        }

        public override string ToString() {
            return String.Format("Rational Player: {0}", Index);
        }
}
    [DebuggerDisplay("{ToString()}")]
    public class HonestPlayer : IActorPlayer<ProofValue<TProof>> {
        public readonly ShareCombiner Brain;
        private HashSet<IPlayer> cooperatingPlayers = null;

        public HonestPlayer(
                AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> scheme,
                Share share) {
            this.Brain = new ShareCombiner(scheme, share);
        }

        public BigInteger? Secret { get { return Brain.Secret; } }
        public int Index { get { return Brain.Share.CommonIndex; } }

        public void Init(IEnumerable<IPlayer> players) {
            this.cooperatingPlayers = new HashSet<IPlayer>(players);
        }
        public Dictionary<IPlayer, ProofValue<TProof>> StartRound(int round) {
            if (!Brain.HaveRoundMessage(round)) return null;
            var msg = Brain.GetRoundMessage(round);
            return cooperatingPlayers.ToDictionary(e => e, e => msg);
        }
        public EndRoundResult EndRound(int round, Dictionary<IPlayer, ProofValue<TProof>> receivedMessages) {
            cooperatingPlayers.RemoveWhere(c => Brain.IsExpectedRoundSenderIndex(round, c.Index) != receivedMessages.ContainsKey(c));
            foreach (var msg in receivedMessages) {
                if (Brain.IsExpectedRoundSenderIndex(round, msg.Key.Index)) {
                    var valid = Brain.Receive(round, msg.Key.Index, msg.Value);
                    if (!valid) cooperatingPlayers.Remove(msg.Key);
                }
            }
            
            Brain.FinishRound(round);
            if (Brain.Secret.HasValue) return new EndRoundResult(Brain.Secret);
            return new EndRoundResult(failed: cooperatingPlayers.Count < Brain.Share.Common.Threshold);
        }

        public override string ToString() {
            return String.Format("Honest Player: {0}", Index);
        }
    }
    public class ShareCombiner {
        public readonly Share Share;
        public readonly AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> Scheme;
        private readonly MessageSequence<ModPoint> reconstructor;

        public ShareCombiner(
                AsyncVerifiedProtocol<TProof, TPublicKey, TPrivateKey> scheme,
                Share share) {
            this.Scheme = scheme;
            this.Share = share;

            this.reconstructor = new MessageSequence<ModPoint>(
                Scheme.wrappedSharingScheme,
                Share.Common.Commitment,
                Share.Common.Threshold,
                Share.Common.Total);
        }

        public BigInteger? Secret { get { return reconstructor.Check(); } }

        [Pure]
        public bool IsExpectedRoundSenderIndex(int round, int senderIndex) {
            Contract.Requires(round >= 0);
            return round % Share.Common.Total == senderIndex;
        }
        [Pure]
        public bool HaveRoundMessage(int round) {
            Contract.Requires(round >= 0);
            return IsExpectedRoundSenderIndex(round, Share.CommonIndex);
        }
        [Pure]
        public ProofValue<TProof> GetRoundMessage(int round) {
            Contract.Requires(round >= 0);
            Contract.Requires(HaveRoundMessage(round));
            return Scheme.GetRoundMessage(round, Share);
        }

        public bool Receive(int round, int senderIndex, ProofValue<TProof> message) {
            Contract.Requires(round >= 0);
            Contract.Requires(IsExpectedRoundSenderIndex(round, senderIndex));

            if (!Scheme.IsMessageValid(round, Share.Common.Nonce, Share.Common.PublicKeys[senderIndex], message)) {
                return false;
            }
            
            var mask = Share.Offsets[senderIndex];
            var shr = ModPoint.From(senderIndex, message.Value + mask);
            reconstructor.Take(round, shr);
            return true;
        }
        public void FinishRound(int round) {
            reconstructor.NoteFinished(round);
        }
    }
}
