using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public static class ProtocolSynchronousBounded {
    public static ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.RationalPlayer ConnectRationalPlayer<TVRFProof, TVRFPublicKey, TVRFPrivateKey>(
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.Share share,
        SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
        return ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.RationalPlayer.FromConnect(scheme, share, net);
    }
    public static ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.RationalColludingPlayer[] ConnectRationalCoalition<TVRFProof, TVRFPublicKey, TVRFPrivateKey>(
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.Share[] shares,
        SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
        return ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.RationalColludingPlayer.FromConnect(scheme, shares, net);
    }
    public static ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.MaliciousPlayer ConnectMaliciousPlayer<TVRFProof, TVRFPublicKey, TVRFPrivateKey>(
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
        ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.Share share,
        SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
            return ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.MaliciousPlayer.FromConnect(scheme, share, net);
    }
}
public class ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> : ISecretSharingScheme<ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey>.Share> {
    public readonly ICommitmentScheme CommitmentScheme;
    public readonly IVerifiableRandomFunctionScheme<TVRFPublicKey, TVRFPrivateKey, TVRFProof> VRFScheme;
    public BigInteger Range { get; private set; }

    public ProtocolSynchronousBounded(
            BigInteger range,
            ICommitmentScheme commitmentScheme,
            IVerifiableRandomFunctionScheme<TVRFPublicKey, TVRFPrivateKey, TVRFProof> vrfScheme) {
        Contract.Requires(range > 0);
        Contract.Requires(commitmentScheme != null);
        Contract.Requires(vrfScheme != null);
        this.CommitmentScheme = commitmentScheme;
        this.Range = range;
        this.VRFScheme = vrfScheme;
    }

    public Share[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        var vrfKeys = Enumerable.Range(0, total).Select(e => VRFScheme.CreatePublicPrivateKeyPair(rng)).ToArray();
        var publicVRFKeys = vrfKeys.Select(e => e.Item1).ToArray();
        var privateVRFKeys = vrfKeys.Select(e => e.Item2).ToArray();

        var targetRound = rng.GenerateNextValuePoisson(new Rational(5, 6));
        var targetRoundMessages = privateVRFKeys.Select(e => VRFScheme.Generate(e, targetRound, Range).Value);
        var targetRoundShares = ShamirSecretSharing.Create(ModInt.From(secret, Range), threshold, total, rng);
        var offsets = targetRoundMessages.Zip(targetRoundShares, (msg, shr) => shr.Y - msg).ToArray();

        var common = new CommonShare(publicVRFKeys,  offsets, CommitmentScheme.Create(secret, rng), threshold);
        return privateVRFKeys.Select((e, i) => new Share(e, common, i)).ToArray();
    }

    public BigInteger?[] Validate(BigInteger round, CommonShare common, ProofValue<TVRFProof>[] messages) {
        return messages.Zip(common.PublicKeys, (m, k) => {
            if (m == null) return (BigInteger?)null;
            if (!VRFScheme.Verify(k, round, Range, m)) return null;
            return m.Value;
        }).ToArray();
    }
    public BigInteger? TryGetSecret(BigInteger round, CommonShare common, Tuple<int, BigInteger?>[] validatedMessages) {
        var roundShares = validatedMessages.Where(e => e != null && e.Item2 != null)
                                           .Select(e => ModPoint.From(e.Item1 + 1, e.Item2.Value + common.Offsets[e.Item1]))
                                           .ToArray();

        if (roundShares.Length < common.Threshold) return null;
        var potentialSecret = ShamirSecretSharing.TryCombineShares(common.Threshold, roundShares);
        if (potentialSecret == null) return null;
        if (!common.Commitment.Matches(potentialSecret.Value)) return null;
        return potentialSecret.Value;
    }

    public class CommonShare {
        public readonly IList<TVRFPublicKey> PublicKeys;
        public readonly IList<ModInt> Offsets;
        public readonly ICommitment Commitment;
        public readonly int Threshold;
        public CommonShare(IList<TVRFPublicKey> publicKeys, IList<ModInt> offsets, ICommitment commitment, int threshold) {
            this.PublicKeys = publicKeys;
            this.Offsets = offsets;
            this.Commitment = commitment;
            this.Threshold = threshold;
        }
    }
    public class Share {
        public TVRFPublicKey PublicKey { get { return Common.PublicKeys[CommonIndex]; } }
        public ModInt Offset { get { return Common.Offsets[CommonIndex]; } }
        public readonly TVRFPrivateKey PrivateKey;
        public readonly CommonShare Common;
        public readonly int CommonIndex;
        public Share(TVRFPrivateKey privateKey, CommonShare common, int commonIndex) {
            this.PrivateKey = privateKey;
            this.Common = common;
            this.CommonIndex = commonIndex;
        }
    }

    public BigInteger Combine(int degree, IList<Share> shares) {
        int round = 0;
        var common = shares.First().Common;
        while (true) {
            var messages = shares.Select(e => Tuple.Create(e.CommonIndex, (BigInteger?)VRFScheme.Generate(e.PrivateKey, round, Range).Value)).ToArray();
            var secret = TryGetSecret(round, common, messages);
            if (secret != null) return secret.Value;
            round += 1;
        }
    }
    public BigInteger? TryCombine(int degree, IList<Share> shares) {
        return Combine(degree, shares);
    }

    public abstract class AbstractPlayer : IPlayer, IRoundActor {
        public int Index { get { return share.CommonIndex; } }

        private readonly Share share;
        private readonly ISyncSocket<IPlayer, ProofValue<TVRFProof>> socket;
        private HashSet<IPlayer> cooperatingPlayers = null;
        private readonly ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme;

        protected AbstractPlayer(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
            this.scheme = scheme;
            this.share = share;
            this.socket = net.Connect(this);
        }

        public virtual IEnumerable<IPlayer> WhoToSendTo(IEnumerable<Tuple<IPlayer, bool>> otherCoopPairs) {
            return otherCoopPairs.Where(e => e.Item2).Select(e => e.Item1);
        }
        public virtual ProofValue<TVRFProof> GetMessageToSend(int round) {
            return scheme.VRFScheme.Generate(share.PrivateKey, round, scheme.Range);
        }
        public virtual IEnumerable<Share> GetAvailableShares() {
            yield return share;
        }
        public virtual IEnumerable<ModPoint> GetLocalShares(int round) {
            foreach (var s in GetAvailableShares())
                yield return ModPoint.From(s.CommonIndex, share.Offset + scheme.VRFScheme.Generate(share.PrivateKey, round, scheme.Range).Value);
        }
        public virtual bool ContinuePlaying(int receivedCount, int shareCount) {
            return shareCount >= share.Common.Threshold;
        }

        public void BeginRound(int round) {
            var otherPlayers = socket.GetParticipants().Where(e => e != this);
            if (cooperatingPlayers == null) cooperatingPlayers = new HashSet<IPlayer>(otherPlayers);
            var message = GetMessageToSend(round);
            var receivers = WhoToSendTo(socket.GetParticipants().Select(e => Tuple.Create(e, cooperatingPlayers.Contains(e))));
            socket.SetMessageToSendTo(receivers, message);
        }
        public EndRoundResult EndRound(int round) {
            var common = share.Common;
            var received = socket.GetReceivedMessages();
            var validReceived = received.Where(e => scheme.VRFScheme.Verify(common.PublicKeys[e.Key.Index], round, scheme.Range, e.Value));
            cooperatingPlayers.IntersectWith(validReceived.Select(e => e.Key));

            var localRoundShares = GetLocalShares(round);
            var receivedRoundShares = validReceived.Select(e => ModPoint.From(e.Key.Index, common.Offsets[e.Key.Index] + e.Value.Value)).ToArray();
            var roundShares = localRoundShares.Concat(receivedRoundShares).DistinctBy(e => e.X).ToArray();
            if (!ContinuePlaying(receivedRoundShares.Length, roundShares.Length)) return new EndRoundResult(failed: true);

            var potentialSecret = ShamirSecretSharing.TryCombineShares(common.Threshold, roundShares);
            if (potentialSecret == null || !common.Commitment.Matches(potentialSecret.Value))
                return new EndRoundResult();

            return new EndRoundResult(potentialSecret);
        }
    }
    public class RationalColludingPlayer : AbstractPlayer {
        private readonly Share[] Shares;
        private RationalColludingPlayer(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                Share[] AllColludingShares,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net)
            : base(scheme, share, net) {
                this.Shares = AllColludingShares;
        }
        public static RationalColludingPlayer[] FromConnect(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share[] shares,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
            return shares.Select(e => new RationalColludingPlayer(scheme, e, shares, net)).ToArray();
        }
        public override IEnumerable<Share> GetAvailableShares() {
            return Shares;
        }
    }
    public class RationalPlayer : AbstractPlayer {
        private RationalPlayer(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) : base(scheme, share, net) {
        }
        public static RationalPlayer FromConnect(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
            return new RationalPlayer(scheme, share, net);
        }
    }
    public class MaliciousPlayer : AbstractPlayer {
        private MaliciousPlayer(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net)
            : base(scheme, share, net) {
        }
        public static MaliciousPlayer FromConnect(
                ProtocolSynchronousBounded<TVRFProof, TVRFPublicKey, TVRFPrivateKey> scheme,
                Share share,
                SyncNetwork<IPlayer, ProofValue<TVRFProof>> net) {
            return new MaliciousPlayer(scheme, share, net);
        }

        public override IEnumerable<IPlayer> WhoToSendTo(IEnumerable<Tuple<IPlayer, bool>> otherCoopPairs) {
            return otherCoopPairs.Select(e => e.Item1);
        }
        public override ProofValue<TVRFProof> GetMessageToSend(int round) {
            return base.GetMessageToSend(round * round + 5000);
        }
        public override IEnumerable<ModPoint> GetLocalShares(int round) {
            return base.GetLocalShares(round);
        }
        public override bool ContinuePlaying(int receivedCount, int shareCount) {
            return shareCount > 1;
        }
    }
}
