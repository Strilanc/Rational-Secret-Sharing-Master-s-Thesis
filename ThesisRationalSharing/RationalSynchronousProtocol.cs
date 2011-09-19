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
        return keys.Select(e => new Share(e.Item1, e.Item2, common)).ToArray();
    }

    public Tuple<TEncryptedMessage> GetRoundMessage(BigInteger round, Share share) {
        return Tuple.Create(publicCryptoScheme.PrivateEncrypt(share.PrivateKey, roundNonceMixingScheme.Mix(round, share.common.Nonce)));
    }
    public Tuple<TEncryptedMessage>[] Validate(BigInteger round, CommonShare common, Tuple<TEncryptedMessage>[] messages) {
        return messages.Zip(common.PublicKeys, (m, k) => {
            if (m == null) return null;
            if (publicCryptoScheme.PublicDecrypt(k, m.Item1) != round + common.Nonce) return null;
            return m;
        }).ToArray();
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
        public readonly TPublicKey PublicKey;
        public readonly TPrivateKey PrivateKey;
        public readonly CommonShare common;
        public Share(TPublicKey publicKey, TPrivateKey privateKey, CommonShare common) {
            this.PublicKey = publicKey;
            this.PrivateKey = privateKey;
            this.common = common;
        }
    }


    public BigInteger Combine(int degree, IList<Share> shares) {
        int i = 0;
        var common = shares.First().common;
        while (true) {
            var messages = shares.Select(e => GetRoundMessage(i, e)).ToArray();
            var secret = TryGetSecret(i, common, messages);
            if (secret != null) return secret.Value;
            i += 1;
        }
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        return Combine(degree, shares);
    }
}
