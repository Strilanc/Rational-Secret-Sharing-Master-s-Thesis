using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public class RationalSynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> : ISharingScheme<RationalSynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>.Share> {
    public readonly ISharingScheme<TWrappedShare> wrappedSharingScheme;
    public readonly IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme;
    public readonly IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme;
    public readonly IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme;

    public RationalSynchronousProtocol(
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
        var targetRoundMessages = keys.Select(e => publicCryptoScheme.PrivateEncrypt(e.Item2, roundNonceMixingScheme.Mix(targetRound, nonce)));
        var wrappedShares = wrappedSharingScheme.Create(secret, threshold, total, rng);
        var shareMasks = targetRoundMessages.Zip(wrappedShares, (msg, shr) => shareMixingScheme.Mix(shr, msg)).ToArray();
        var common = new CommonShare(
            nonce: nonce, 
            publicKeys: keys.Select(e => e.Item1).ToArray(), 
            masks: shareMasks, 
            commitment: HashCommitment.FromValueAndGeneratedSalt(secret, rng),
            threshold: threshold);
        return keys.Select((e, i) => new Share(e.Item2, common, i)).ToArray();
    }

    public TEncryptedMessage GetRoundMessage(BigInteger round, Share share) {
        return publicCryptoScheme.PrivateEncrypt(share.PrivateKey, roundNonceMixingScheme.Mix(round, share.Common.Nonce));
    }
    public Tuple<TEncryptedMessage>[] Validate(BigInteger round, CommonShare common, Tuple<TEncryptedMessage>[] messages) {
        return messages.Zip(common.PublicKeys, (m, k) => {
            if (m == null) return null;
            if (!IsMessageValid(round, common.Nonce, k, m.Item1)) return null;
            return m;
        }).ToArray();
    }
    public bool IsMessageValid(BigInteger round, BigInteger nonce, TPublicKey key, TEncryptedMessage message) {
        return publicCryptoScheme.PublicDecrypt(key, message) == roundNonceMixingScheme.Mix(round, nonce);
    }
    public BigInteger? TryGetSecret(BigInteger round, CommonShare common, Tuple<TEncryptedMessage>[] validatedMessages) {
        var roundShares = validatedMessages.Zip(common.Masks, (v, m) => v == null ? null : Tuple.Create(shareMixingScheme.Unmix(m, v.Item1)))
                                           .Where(e => e != null)
                                           .Select(e => e.Item1)
                                           .ToArray();

        if (roundShares.Length < common.Threshold) return null;
        var potentialSecret = wrappedSharingScheme.TryCombine(common.Threshold, roundShares);
        if (potentialSecret == null) return null;
        if (!common.Commitment.Matches(potentialSecret.Value)) return null;
        return potentialSecret.Value;
    }

    public class CommonShare {
        public readonly BigInteger Nonce;
        public readonly IList<TPublicKey> PublicKeys;
        public readonly IList<TWrappedShare> Masks;
        public readonly ICommitment Commitment;
        public readonly int Threshold;
        public CommonShare(BigInteger nonce, IList<TPublicKey> publicKeys, IList<TWrappedShare> masks, ICommitment commitment, int threshold) {
            this.Nonce = nonce;
            this.PublicKeys = publicKeys;
            this.Masks = masks;
            this.Commitment = commitment;
            this.Threshold = threshold;
        }
    }
    public class Share {
        public TPublicKey PublicKey { get { return Common.PublicKeys[CommonIndex]; } }
        public TWrappedShare Mask { get { return Common.Masks[CommonIndex]; } }
        public readonly TPrivateKey PrivateKey;
        public readonly CommonShare Common;
        public readonly int CommonIndex;
        public Share(TPrivateKey privateKey, CommonShare common, int commonIndex) {
            this.PrivateKey = privateKey;
            this.Common = common;
            this.CommonIndex = commonIndex;
        }
    }


    public BigInteger Combine(int degree, IList<Share> shares) {
        int i = 0;
        var common = shares.First().Common;
        while (true) {
            var messages = shares.Select(e => Tuple.Create(GetRoundMessage(i, e))).ToArray();
            var secret = TryGetSecret(i, common, messages);
            if (secret != null) return secret.Value;
            i += 1;
        }
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        return Combine(degree, shares);
    }

    public interface IPlayer {
        TWrappedShare Mask { get; }
        TPublicKey PublicKey { get; }
    }
    public class RationalPlayer : IPlayer, IRoundActor {
        public TWrappedShare Mask { get { return share.Mask; } }
        public TPublicKey PublicKey { get { return share.PublicKey; } }

        public readonly Share share;
        public readonly ISyncSocket<IPlayer, TEncryptedMessage> socket;
        private HashSet<IPlayer> cooperatingPlayers = null;
        private readonly RationalSynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme;

        private RationalPlayer(
                RationalSynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, TEncryptedMessage> net) {
            this.scheme = scheme;
            this.share = share;
            this.socket = net.Connect(this);
        }
        public static RationalPlayer FromConnect(
                RationalSynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, TEncryptedMessage> net) {
            return new RationalPlayer(scheme, share, net);
        }

        public void BeginRound(int round) {
            if (cooperatingPlayers == null) cooperatingPlayers = new HashSet<IPlayer>(socket.GetParticipants());
            socket.SetMessageToSendTo(cooperatingPlayers, scheme.GetRoundMessage(round, share));
        }
        public ActorEndRoundResult EndRound(int round) {
            Contract.Ensures(!Contract.Result<ActorEndRoundResult>().OptionalResult.HasValue || Contract.Result<ActorEndRoundResult>().Finished);
            var common = share.Common;
            var received = socket.GetReceivedMessages();
            var validReceived = received.Where(e => scheme.IsMessageValid(round, common.Nonce, e.Key.PublicKey, e.Value));
            
            cooperatingPlayers.IntersectWith(validReceived.Select(e => e.Key));
            if (cooperatingPlayers.Count < common.Threshold) return new ActorEndRoundResult(finished: true);

            var roundShares = validReceived.Select(e => scheme.shareMixingScheme.Unmix(e.Key.Mask, e.Value)).ToArray();
            var potentialSecret = scheme.wrappedSharingScheme.TryCombine(common.Threshold, roundShares);
            if (potentialSecret == null || !common.Commitment.Matches(potentialSecret.Value))
                return new ActorEndRoundResult();

            return new ActorEndRoundResult(finished: true, optionalResult: potentialSecret);
        }
    }
}
