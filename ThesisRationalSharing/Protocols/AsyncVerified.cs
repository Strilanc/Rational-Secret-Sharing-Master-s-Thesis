using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> : ISharingScheme<AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>.Share> {
    public readonly ISharingScheme<TWrappedShare> wrappedSharingScheme;
    public readonly IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme;
    public readonly IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme;
    public readonly IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme;

    public AsyncVerifiedProtocol(
            ISharingScheme<TWrappedShare> wrappedSharingScheme, 
            IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme,
            IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme,
            IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme) {
        Contract.Requires(wrappedSharingScheme != null);
        Contract.Requires(publicCryptoScheme != null);
        Contract.Requires(shareMixingScheme != null);
        Contract.Requires(roundNonceMixingScheme != null);
        this.wrappedSharingScheme = wrappedSharingScheme;
        this.publicCryptoScheme = publicCryptoScheme;
        this.shareMixingScheme = shareMixingScheme;
        this.roundNonceMixingScheme = roundNonceMixingScheme;
    }

    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        var nonce = rng.GenerateNextValueMod(BigInteger.One << 128);
        var targetRound = rng.GenerateNextValuePoisson(5, 6);
        var keys = Enumerable.Range(0, total).Select(e => publicCryptoScheme.GenerateKeyPair(rng)).ToArray();
        var common = new CommonShare(
            nonce: nonce, 
            publicKeys: keys.Select(e => e.Item1).ToArray(), 
            commitment: HashCommitment.FromValueAndGeneratedSalt(secret, rng),
            threshold: threshold,
            total: total);

        var firstLearnerIndex = (targetRound + threshold - 2) % total;
        return keys.Select((e, i) => {
            var firstUnmaskingRound = targetRound + (i - firstLearnerIndex).ProperMod(total);
            var wrappedShares = wrappedSharingScheme.Create(secret, threshold, total, rng);
            var x1 = from round in Enumerable.Range((int)firstUnmaskingRound, total)
                     let senderId = round % total
                     let senderKey = keys[senderId].Item2
                     let baseMessage = roundNonceMixingScheme.Mix(round, nonce)
                     let encryptedMessage = publicCryptoScheme.PrivateEncrypt(senderKey, baseMessage)
                     let wrappedShare = wrappedShares[senderId]
                     let maskedShare = shareMixingScheme.Mix(wrappedShare, encryptedMessage)
                     select new { round = round, senderId = senderId, senderKey = senderKey, baseMessage = baseMessage, encryptedMessage = encryptedMessage, wrappedShare = wrappedShare, maskedShare = maskedShare };
            var x2 = x1.ToArray();
            var shareMasks = x1.Select(x => x.maskedShare).Rotate((int)firstUnmaskingRound).ToArray();
            return new Share(e.Item2, shareMasks, common, i);
        }).ToArray();
    }

    public TEncryptedMessage GetRoundMessage(BigInteger round, Share share) {
        return publicCryptoScheme.PrivateEncrypt(share.PrivateKey, roundNonceMixingScheme.Mix(round, share.Common.Nonce));
    }
    public bool IsMessageValid(BigInteger round, BigInteger nonce, TPublicKey key, TEncryptedMessage message) {
        return publicCryptoScheme.PublicDecrypt(key, message) == roundNonceMixingScheme.Mix(round, nonce);
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
        public readonly IList<TWrappedShare> Masks;
        public readonly TPrivateKey PrivateKey;
        public readonly CommonShare Common;
        public readonly int CommonIndex;
        public Share(TPrivateKey privateKey, IList<TWrappedShare> masks, CommonShare common, int commonIndex) {
            this.PrivateKey = privateKey;
            this.Common = common;
            this.CommonIndex = commonIndex;
            this.Masks = masks;
        }
        public override string ToString() {
            return String.Format("Id: {0}, PrivateKey: {1}, PublicKey: {2}", CommonIndex, PrivateKey, PublicKey);
        }
    }


    public BigInteger Combine(int degree, IList<Share> shares) {
        Contract.Requires<ArgumentException>(shares.GroupBy(e => e.Common).Count() == 1);
        Contract.Requires<ArgumentException>(degree == shares.First().Common.Threshold);
        var dares = shares.ToDictionary(e => e.CommonIndex, e => e);
        var s = shares.First();
        var total = s.Common.Total;
        int round = 0;
        var q = new Queue<TWrappedShare>();
        while (true) {
            var i = round % total;
            if (!dares.ContainsKey(i)) continue;
            
            var msg = GetRoundMessage(round, dares[i]);
            var msk = s.Masks[i];
            var shr = shareMixingScheme.Unmix(msk, msg);
            q.Enqueue(shr);
            
            if (q.Count > degree) q.Dequeue();
            var n = wrappedSharingScheme.TryCombine(degree, q.ToArray());
            if (n.HasValue && s.Common.Commitment.Matches(n.Value)) return n.Value;

            round += 1;
        }
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        return Combine(degree, shares);
    }

    [DebuggerDisplay("{ToString()}")]
    public class RationalPlayer : IPlayer, AsyncNetwork<IPlayer, TEncryptedMessage>.IActor {
        public int Index { get { return share.CommonIndex; } }

        public readonly Share share;
        private HashSet<IPlayer> cooperatingPlayers = null;
        private readonly Queue<TWrappedShare> lastReceivedShares = new Queue<TWrappedShare>();
        private readonly AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme;
        private Tuple<TEncryptedMessage> roundMessage = null;

        public RationalPlayer(
                AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share) {
            this.scheme = scheme;
            this.share = share;
        }

        public void Init(IEnumerable<IPlayer> players) {
            cooperatingPlayers = new HashSet<IPlayer>(players);
        }

        public Dictionary<IPlayer, TEncryptedMessage> GetRoundMessages(int round) {
            if (round % share.Common.Total != share.CommonIndex)
                return new Dictionary<IPlayer, TEncryptedMessage>(); // not our turn to send
            return cooperatingPlayers.ToDictionary(e => e, e => scheme.GetRoundMessage(round, share));
        }

        public EndRoundResult EndRound(int round) {
            Contract.Ensures(!Contract.Result<EndRoundResult>().OptionalResult.HasValue || Contract.Result<EndRoundResult>().Finished);

            try {
                var sender = cooperatingPlayers.Where(e => e.Index == round % share.Common.Total).FirstOrDefault();
                if (sender == null) return new EndRoundResult();

                if (roundMessage == null) {
                    cooperatingPlayers.Remove(sender);
                    return new EndRoundResult(cooperatingPlayers.Count < share.Common.Threshold);
                }

                var mask = share.Masks[sender.Index];
                var ms = scheme.shareMixingScheme.Unmix(mask, roundMessage.Item1);
                lastReceivedShares.Enqueue(ms);
                if (lastReceivedShares.Count > share.Common.Threshold) lastReceivedShares.Dequeue();

                var potentialSecret = scheme.wrappedSharingScheme.TryCombine(share.Common.Threshold, lastReceivedShares.ToArray());
                if (potentialSecret == null || !share.Common.Commitment.Matches(potentialSecret.Value))
                    return new EndRoundResult();
                
                return new EndRoundResult(finished: true, optionalResult: potentialSecret);
            } finally {
                roundMessage = null;
            }
        }
        public void ReceiveMessage(int round, IPlayer sender, TEncryptedMessage message) {
            if (!cooperatingPlayers.Contains(sender)
                    || sender.Index != round % share.Common.Total
                    || !scheme.IsMessageValid(round, share.Common.Nonce, share.Common.PublicKeys[sender.Index], message)) {
                cooperatingPlayers.Remove(sender);
            } else {
                roundMessage = Tuple.Create(message);
            }
        }
        public override string ToString() {
            return String.Format("Id: {0}", Index);
        }
    }
}
