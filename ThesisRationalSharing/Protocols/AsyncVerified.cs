using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class AsyncVerifiedProtocol {
    public static AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> From<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>(
            ISecretSharingScheme<TWrappedShare> wrappedSharingScheme,
            IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme,
            IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme,
            IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme) {
        Contract.Requires(wrappedSharingScheme != null);
        Contract.Requires(publicCryptoScheme != null);
        Contract.Requires(shareMixingScheme != null);
        Contract.Requires(roundNonceMixingScheme != null);
        return new AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>(wrappedSharingScheme, publicCryptoScheme, shareMixingScheme, roundNonceMixingScheme);
    }
}
public class AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> : ISecretSharingScheme<AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey>.Share> {
    public readonly ISecretSharingScheme<TWrappedShare> wrappedSharingScheme;
    public readonly IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncryptedMessage> publicCryptoScheme;
    public readonly IReversibleMixingScheme<TWrappedShare, TEncryptedMessage> shareMixingScheme;
    public readonly IMixingScheme<BigInteger, BigInteger> roundNonceMixingScheme;

    /// <param name="wrappedSharingScheme">Underlying augmented sharing scheme.</param>
    /// <param name="publicCryptoScheme">Encrypts deterministic base round messages.</param>
    /// <param name="shareMixingScheme">Used to reversibly mask true shares using round messages.</param>
    /// <param name="roundNonceMixingScheme">Scrambles the round using a nonce. Output must match input for publicCryptoScheme encryption.</param>
    public AsyncVerifiedProtocol(
            ISecretSharingScheme<TWrappedShare> wrappedSharingScheme, 
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
        var keys = Enumerable.Range(0, total).Select(e => publicCryptoScheme.GeneratePublicPrivateKeyPair(rng)).ToArray();
        var common = new CommonShare(
            nonce: nonce, 
            publicKeys: keys.Select(e => e.Item1).ToArray(), 
            commitment: HashCommitment.FromValueAndGeneratedSalt(secret, rng),
            threshold: threshold,
            total: total);

        var firstLearnerIndex = (targetRound + threshold - 2) % total;
        return keys.Select((k, i) => {
            var firstUnmaskingRound = (int)(targetRound + (i - firstLearnerIndex).ProperMod(total));
            var wrappedShares = wrappedSharingScheme.Create(secret, threshold, total, rng);
            var shareMasks = from round in Enumerable.Range(firstUnmaskingRound, total)
                             let senderId = round % total
                             let senderKey = keys[senderId].Item2
                             let baseMessage = roundNonceMixingScheme.Mix(round, nonce)
                             let encryptedMessage = publicCryptoScheme.PrivateEncrypt(senderKey, baseMessage)
                             let wrappedShare = wrappedShares[senderId]
                             select shareMixingScheme.Mix(wrappedShare, encryptedMessage);
            var alignedShareMasks = shareMasks.Rotate(firstUnmaskingRound).ToArray();
            return new Share(k.Item2, alignedShareMasks, common, i);
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
        if (shares.GroupBy(e => e.Common).Count() > 1) throw new ArgumentException("Unrelated shares.");
        if (degree != shares.First().Common.Threshold) throw new ArgumentException("Shares of different degree.");

        var shareMap = shares.ToDictionary(e => e.CommonIndex, e => e);
        var usedShare = shares.First();
        var total = usedShare.Common.Total;
        var wrappedShareQueue = new Queue<TWrappedShare>();

        int round = 0;
        while (true) {
            var i = round % total;
            if (!shareMap.ContainsKey(i)) {
                round += 1;
                continue;
            }
            
            var msg = GetRoundMessage(round, shareMap[i]);
            var msk = usedShare.Masks[i];
            var shr = shareMixingScheme.Unmix(msk, msg);
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
        return new RationalCoalition(this, shares.ToArray());
    }
    [DebuggerDisplay("{ToString()}")]
    public class RationalCoalition {
        private readonly Colluder[] Colluders;
        private BigInteger? secret = null;
        public readonly AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme;

        public IEnumerable<Share> Shares() {
            return Colluders.Select(e => e.Hidden.h.share);
        }
        public IEnumerable<IActorPlayer<TEncryptedMessage>> GetPlayers() {
            return Colluders;
        }

        [DebuggerDisplay("{ToString()}")]
        private class Colluder : IActorPlayer<TEncryptedMessage> {
            public readonly RationalPlayer Hidden;
            public readonly RationalCoalition Coalition;
            public readonly bool IsSilent;

            public Colluder(RationalPlayer hidden, RationalCoalition coalition, bool IsSilent) {
                this.Hidden = hidden;
                this.Coalition = coalition;
                this.IsSilent = IsSilent;
            }

            public int Index { get { return Hidden.Index; } }
            public void Init(IEnumerable<IPlayer> players) {
                Hidden.Init(players);
            }
            public Dictionary<IPlayer, TEncryptedMessage> StartRound(int round) {
                for (int i = 0; i < Coalition.Shares().First().Common.Total; i++)
                    Coalition.TrySneakRound(i + round);
                if (Coalition.secret.HasValue) return null;
                var r = Hidden.StartRound(round);
                return IsSilent ? null : r;
            }
            public EndRoundResult EndRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
                if (Coalition.secret.HasValue) return new EndRoundResult(optionalResult: Coalition.secret);
                var r = Hidden.EndRound(round, receivedMessages);
                if (r.OptionalResult.HasValue) Coalition.secret = r.OptionalResult;
                return r;
            }

            public override string ToString() {
                return "Colluder " + Index + " of " + Coalition;
            }
        }

        public RationalCoalition(AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme, Share[] shares) {
            Contract.Requires(scheme != null);
            Contract.Requires(shares != null);
            Contract.Requires(shares.Length >= 1);
            Contract.Requires(shares.Select(e => e.Common).Distinct().IsSingle());
            Contract.Requires(shares.Select(e => e.CommonIndex).Duplicates().None());
            this.Colluders = shares.Select((e, i) => new Colluder(new RationalPlayer(new HonestPlayer(scheme, e)), this, i == 0)).ToArray();
            this.scheme = scheme;
            var c = shares.First().Common;
            if (shares.Length >= c.Threshold) {
                this.secret = scheme.Combine(c.Threshold, shares);
            }
        }

        private void TrySneakRound(int round) {
            if (secret.HasValue) return;
            var p = Colluders.FirstOrDefault(e => e.Index == round % Shares().First().Common.Total);
            if (p == null) return;
            var msg = new Dictionary<IPlayer, TEncryptedMessage>() { 
                { p,  scheme.GetRoundMessage(round, p.Hidden.h.share) } 
            };
            foreach (var r in Colluders) {
                secret = r.Hidden.PeekRound(round, msg).OptionalResult;
                if (secret.HasValue) return;
            }
        }

        public override string ToString() {
            return "Rational Coalition: {" + String.Join(", ", Colluders.Select(e => e.Index)) + "}";
        }
    }
    [DebuggerDisplay("{ToString()}")]
    public class SilentPlayer : IActorPlayer<TEncryptedMessage> {
        public SilentPlayer(int index) { this.Index = index; }

        public int Index { get; private set; }
        public void Init(IEnumerable<IPlayer> players) {
        }
        public Dictionary<IPlayer, TEncryptedMessage> StartRound(int round) {
            return new Dictionary<IPlayer, TEncryptedMessage>();
        }
        public EndRoundResult EndRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            return new EndRoundResult(finished: true);
        }

        public override string ToString() {
            return "SilentPlayer (ID: " + Index + ")";
        }
    }
    public RationalPlayer MakeRationalPlayer(Share share) {
        return new RationalPlayer(new HonestPlayer(this, share));
    }
    [DebuggerDisplay("{ToString()}")]
    public class RationalPlayer : IActorPlayer<TEncryptedMessage> {
        public readonly HonestPlayer h;

        public RationalPlayer(HonestPlayer h) {
            Contract.Requires(h != null);
            this.h = h;
        }

        public int Index { get { return h.Index; } }
        public void Init(IEnumerable<IPlayer> players) {
            h.Init(players);
        }
        public Dictionary<IPlayer, TEncryptedMessage> StartRound(int round) {
            if (h.Secret.HasValue) return null;
 	        return h.StartRound(round);
        }
        public EndRoundResult PeekRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            if (h.Secret.HasValue) return new EndRoundResult(optionalResult: h.Secret);
            return h.PeekRound(round, receivedMessages);
        }
        public EndRoundResult EndRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            if (h.Secret.HasValue) return new EndRoundResult(optionalResult: h.Secret);
            return h.EndRound(round, receivedMessages);
        }

        public override string ToString() {
            return String.Format("Rational Player (Id: {0})", Index);
        }
}
    public class HonestPlayer : IActorPlayer<TEncryptedMessage> {
        public int Index { get { return share.CommonIndex; } }
        private readonly MessageSequence<TWrappedShare> ms;
        public readonly Share share;
        private readonly AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme;
        private HashSet<IPlayer> cooperatingPlayers = null;
        public BigInteger? Secret { get { return ms.Check(); } }

        public HonestPlayer(
                AsyncVerifiedProtocol<TWrappedShare, TEncryptedMessage, TPublicKey, TPrivateKey> scheme,
                Share share) {
            this.scheme = scheme;
            this.share = share;
            this.ms = new MessageSequence<TWrappedShare>(
                scheme.wrappedSharingScheme,
                share.Common.Commitment,
                share.Common.Threshold,
                share.Common.Total);
        }

        public void Init(IEnumerable<IPlayer> players) {
            cooperatingPlayers = new HashSet<IPlayer>(players);
        }
        public Dictionary<IPlayer, TEncryptedMessage> StartRound(int round) {
            if (round % share.Common.Total != share.CommonIndex)
                return new Dictionary<IPlayer, TEncryptedMessage>(); // not our turn to send
            return cooperatingPlayers.ToDictionary(e => e, e => scheme.GetRoundMessage(round, share));
        }
        public EndRoundResult PeekRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            EndRoundHelper(round, receivedMessages);
            return new EndRoundResult(
                finished: cooperatingPlayers.Count < share.Common.Threshold,
                optionalResult: ms.Check());
        }
        public EndRoundResult EndRound(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            EndRoundHelper(round, receivedMessages);
            ms.NoteFinished(round);
            return new EndRoundResult(
                finished: cooperatingPlayers.Count < share.Common.Threshold,
                optionalResult: ms.Check());
        }
        private void EndRoundHelper(int round, Dictionary<IPlayer, TEncryptedMessage> receivedMessages) {
            if (ms.Check().HasValue) return;

            var expectedSenderIndex = round % share.Common.Total;
            foreach (var unexpected in receivedMessages.Keys.Where(e => e.Index != expectedSenderIndex))
                cooperatingPlayers.Remove(unexpected);

            var expectedKey = receivedMessages.Keys.Where(e => e.Index == expectedSenderIndex).FirstOrDefault();
            if (expectedKey == null || !cooperatingPlayers.Contains(expectedKey)) {
                cooperatingPlayers.RemoveWhere(e => e.Index == expectedSenderIndex);
                return;
            }

            var message = receivedMessages[expectedKey];
            if (!scheme.IsMessageValid(round, share.Common.Nonce, share.Common.PublicKeys[expectedSenderIndex], message)) {
                cooperatingPlayers.Remove(expectedKey);
                return;
            }
            var mask = share.Masks[expectedSenderIndex];
            var shr = scheme.shareMixingScheme.Unmix(mask, message);
            ms.Take(round, shr);
        }

        public override string ToString() {
            return String.Format("Honest Player (Id: {0})", Index);
        }
    }
}
