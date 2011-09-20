using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public class RationalAsynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> : ISharingScheme<RationalAsynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>.Share> {
    public readonly ISharingScheme<TWrappedShare> wrappedSharingScheme;
    public readonly IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme;
    public readonly IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme;
    public readonly IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme;

    public RationalAsynchronousProtocol(
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
        var minRound = threshold - 2;
        var targetRound = minRound + rng.GenerateNextValuePoisson(5, 6);
        var keys = Enumerable.Range(0, total).Select(e => publicCryptoScheme.GenerateKeyPair(rng)).ToArray();
        var common = new CommonShare(
            nonce: nonce, 
            publicKeys: keys.Select(e => e.Item1).ToArray(), 
            commitment: HashCommitment.FromValueAndGeneratedSalt(secret, rng),
            threshold: threshold,
            total: total);
        return keys.Select((e, i) => {
            var wrappedShares = wrappedSharingScheme.Create(secret, threshold, total, rng);
            var x1 = keys.Select((f, j) => publicCryptoScheme.PrivateEncrypt(f.Item2, roundNonceMixingScheme.Mix(targetRound + i + j - minRound, nonce)))
                .ToArray();
            var shareMasks = x1
                                 .Zip(wrappedShares, (msg, shr) => shareMixingScheme.Mix(shr, msg))
                                 .ToArray();
            return new Share(e.Item2, shareMasks, common, i);
        }).ToArray();
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
    public BigInteger? TryGetSecret(BigInteger round, Share share, Tuple<TEncryptedMessage>[] validatedMessages) {
        var roundShares = validatedMessages.Zip(share.Masks, (v, m) => v == null ? null : Tuple.Create(shareMixingScheme.Unmix(m, v.Item1)))
                                           .Where(e => e != null)
                                           .Select(e => e.Item1)
                                           .ToArray();

        if (roundShares.Length < share.Common.Threshold) return null;
        var potentialSecret = wrappedSharingScheme.TryCombine(share.Common.Threshold, roundShares);
        if (potentialSecret == null) return null;
        if (!share.Common.Commitment.Matches(potentialSecret.Value)) return null;
        return potentialSecret.Value;
    }

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
    }
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
    }


    public BigInteger Combine(int degree, IList<Share> shares) {
        throw new NotImplementedException();
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        throw new NotImplementedException();
    }

    public interface IPlayer {
        int Index { get; }
        TPublicKey PublicKey { get; }
    }
    public class RationalPlayer : IPlayer, ITrigger {
        public TPublicKey PublicKey { get { return share.PublicKey; } }
        public int Index { get { return share.CommonIndex; } }

        public readonly Share share;
        public readonly ISyncSocket<IPlayer, TEncryptedMessage> socket;
        private Dictionary<IPlayer, Tuple<TEncryptedMessage>> cooperatingPlayersLastMessage = null;
        private readonly RationalAsynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme;

        private RationalPlayer(
                RationalAsynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, TEncryptedMessage> net) {
            this.scheme = scheme;
            this.share = share;
            this.socket = net.Connect(this);
        }
        public static RationalPlayer FromConnect(
                RationalAsynchronousProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, TEncryptedMessage> net) {
            return new RationalPlayer(scheme, share, net);
        }

        public void BeginRound(int round) {
            if (cooperatingPlayersLastMessage == null) cooperatingPlayersLastMessage = socket.GetParticipants().ToDictionary(e => e, e => (Tuple<TEncryptedMessage>)null);
            if (round % share.Common.Total != share.CommonIndex)
                return; // not our turn to send
            socket.SetMessageToSendTo(cooperatingPlayersLastMessage.Keys, scheme.GetRoundMessage(round, share));
        }
        public Tuple<bool, BigInteger?> EndRound(int round) {
            var common = share.Common;
            var expectedSender = socket.GetParticipants().Single(e => e.Index == round % share.Common.Total);
            var received = socket.GetReceivedMessages();
            if (!received.ContainsKey(expectedSender) || !scheme.IsMessageValid(round, share.Common.Nonce, expectedSender.PublicKey, received[expectedSender])) {
                cooperatingPlayersLastMessage.Remove(expectedSender);
                return Tuple.Create(cooperatingPlayersLastMessage.Count >= share.Common.Threshold, default(BigInteger?));
            }
            cooperatingPlayersLastMessage[expectedSender] = Tuple.Create(received[expectedSender]);

            if (round < common.Threshold - 2) return Tuple.Create(true, default(BigInteger?));
            var ms = Enumerable.Range(round - common.Threshold + 2, share.Common.Total)
                               .Select(e => e % share.Common.Total)
                               .Select(e => {
                                   var message = cooperatingPlayersLastMessage.SingleOrDefault(f => f.Key.Index == e);
                                   if (message.Value == null) return null;
                                   var mask = share.Masks[e];
                                   return Tuple.Create(scheme.shareMixingScheme.Unmix(mask, message.Value.Item1));
                               }).Where(e => e != null)
                               .Select(e => e.Item1)
                               .ToArray();
            var potentialSecret = scheme.wrappedSharingScheme.TryCombine(common.Threshold, ms.Take(common.Threshold).ToArray());
            if (potentialSecret == null || !common.Commitment.Matches(potentialSecret.Value))
                return Tuple.Create(true, default(BigInteger?));

            return Tuple.Create(false, (BigInteger?)potentialSecret.Value);
        }
    }

    public static Dictionary<T, BigInteger> TryRun<T>(SyncNetwork<IPlayer, BigInteger> net, IEnumerable<T> triggers) 
            where T : ITrigger {
        int round = 0;
        var result = new Dictionary<T, BigInteger>();
        var active = new HashSet<T>(triggers);
        while (active.Except(result.Keys).Any()) {
            net.StartRound();
            foreach (var t in active) {
                t.BeginRound(round);
            }
            net.EndRound();
            foreach (var t in active.ToArray()) {
                var r = t.EndRound(round);
                if (!r.Item1) active.Remove(t);
                if (r.Item2.HasValue) result[t] = r.Item2.Value;
            }

            round += 1;
        }
        return result;
    }
}
