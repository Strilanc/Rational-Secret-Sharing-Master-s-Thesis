using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ProtocolTest {
        [TestMethod()]
        public void TryAsyncVerifiedShare() {
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var publicKeySystem = new RSA(997, 991);
            var shamir = new ShamirSecretSharing(1009);
            var shareMixer = new ShamirMixer();
            var roundNonceMixer = new ModMixer(997 * 991);
            var degree = 2;
            var total = 3;

            var scheme = new AsyncVerifiedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>(shamir, publicKeySystem, shareMixer, roundNonceMixer);
            for (int secret = 0; secret < 5; secret++) {
                var shares = scheme.Create(secret, degree, total, randomNumberGenerator);
                var combinedSecret = scheme.Combine(degree, shares);
                var players = shares.Select(e => new AsyncVerifiedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>.RationalPlayer(scheme, e)).ToArray();
                var playerRunSecrets = AsyncNetwork<IPlayer, BigInteger>.Run(players, randomNumberGenerator);

                Assert.IsTrue(combinedSecret == secret);
                Assert.IsTrue(playerRunSecrets.Count == total - 1);
                Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
            }
        }
        [TestMethod()]
        public void TrySyncShare() {
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var publicKeySystem = new RSA(997, 991);
            var shamir = new ShamirSecretSharing(1009);
            var shareMixer = new ShamirMixer();
            var roundNonceMixer = new ModMixer(997 * 991);
            var degree = 2;
            var total = 3;

            var scheme = new SyncedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>(shamir, publicKeySystem, shareMixer, roundNonceMixer);
            for (int secret = 0; secret < 5; secret++) {
                var shares = scheme.Create(secret, degree, total, randomNumberGenerator);
                var combinedSecret = scheme.Combine(degree, shares);

                var net = new SyncNetwork<IPlayer, BigInteger>();
                var players = shares.Select(e => SyncedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>.RationalPlayer.FromConnect(scheme, e, net)).ToArray();
                var playerRunSecrets = net.Run(players);

                Assert.IsTrue(combinedSecret == secret);
                Assert.IsTrue(playerRunSecrets.Count == total);
                Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
            }
        }
    }
}
