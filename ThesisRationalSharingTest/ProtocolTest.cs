using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ProtocolTest {
        [TestMethod()]
        public void TryAsyncVerifiedShare() {
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var publicKeySystem = new RSA(997, 991);
            var shamir = new ShamirSecretSharing(1009);
            var degree = 2;
            var total = 3;

            var scheme = AsyncVerifiedProtocol.From(shamir, publicKeySystem);
            for (int secret = 0; secret < 5; secret++) {
                var shares = scheme.Create(secret, degree, total, randomNumberGenerator);
                var combinedSecret = scheme.Combine(degree, shares);
                var players = shares.Select(e => scheme.MakeRationalPlayer(e)).ToArray();
                var playerRunSecrets = AsyncNetwork<IPlayer, IActorPlayer<ProofValue<BigInteger>>, ProofValue<BigInteger>>.Run(1, players, maxRound: 100);

                Assert.IsTrue(combinedSecret == secret);
                Assert.IsTrue(playerRunSecrets.Count == total - 1);
                Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
            }
        }
        [TestMethod()]
        public void TryAsyncVerifiedCoalitionShare() {
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var publicKeySystem = new RSA(997, 991);
            var shamir = new ShamirSecretSharing(1009);
            var degree = 4;
            var total = 10;
            var coalitionSize = 3;
            var runs = 5;

            var scheme = new AsyncVerifiedProtocol<BigInteger, RSA.Key, RSA.Key>(shamir, publicKeySystem);
            var coalitionWinCount = 0;
            var independentWinCount = 0;
            for (int secret = 0; secret < runs; secret++) {
                var shares = scheme.Create(secret, degree, total, randomNumberGenerator).Shuffle(randomNumberGenerator);
                var combinedSecret = scheme.Combine(degree, shares);
                var independents = shares.Skip(coalitionSize).Select(e => scheme.MakeRationalPlayer(e)).ToArray();
                var coalition = scheme.MakeRationalCoalition(shares.Take(coalitionSize));
                var playerRunSecrets = AsyncNetwork<IPlayer, IActorPlayer<ProofValue<BigInteger>>, ProofValue<BigInteger>>.Run(1, coalition.GetPlayers().Concat(independents), maxRound: 100);

                Assert.IsTrue(combinedSecret == secret);
                Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
                foreach (var x in playerRunSecrets) {
                    if (x.Key is AsyncVerifiedProtocol<BigInteger, RSA.Key, RSA.Key>.RationalPlayer) {
                        independentWinCount += 1;
                    } else {
                        coalitionWinCount += 1;
                    }
                }
            }

            Assert.IsTrue(coalitionWinCount == coalitionSize * runs);
            Assert.IsTrue(independentWinCount < (total - coalitionSize) * runs);
        }
        [TestMethod()]
        public void TrySyncShare() {
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var publicKeySystem = new RSA(997, 991);
            var range = 1009;
            var threshold = 6;
            var total = 10;

            var scheme = new ProtocolSynchronousBounded<BigInteger, RSA.Key, RSA.Key>(range, new CommitSHA1Scheme(), publicKeySystem);
            for (int secret = 0; secret < 25; secret++) {
                var shares = scheme.Create(secret, threshold, total, randomNumberGenerator).Shuffle(randomNumberGenerator);
                Assert.IsTrue(scheme.Combine(threshold, shares) == secret);

                var net = new SyncNetwork<IPlayer, ProofValue<BigInteger>>();
                var numColluding = (int)randomNumberGenerator.GenerateNextValueMod(Math.Min(threshold, total));
                var numMalicious = (int)randomNumberGenerator.GenerateNextValueMod(total - numColluding);
                
                var colludingPlayers = ProtocolSynchronousBounded.ConnectRationalCoalition(scheme, shares.Take(numColluding).ToArray(), net);
                var maliciousPlayers = shares.Skip(numColluding).Take(numMalicious).Select(e => ProtocolSynchronousBounded.ConnectMaliciousPlayer(scheme, e, net)).ToArray();
                var rationalPlayers = shares.Skip(numColluding + numMalicious).Select(e => ProtocolSynchronousBounded.ConnectRationalPlayer(scheme, e, net)).ToArray();

                var players = maliciousPlayers.Cast<IRoundActor>().Concat(rationalPlayers).Concat(colludingPlayers).ToArray();
                var playerRunSecrets = net.Run(players);

                Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
                if (total - numMalicious < threshold) {
                    Assert.IsTrue(playerRunSecrets.Count == 0);
                } else {
                    Assert.IsTrue(colludingPlayers.All(e => playerRunSecrets.ContainsKey(e)));
                    Assert.IsTrue(rationalPlayers.All(e => playerRunSecrets.ContainsKey(e)));
                }
            }
        }
        [TestMethod()]
        public void TryAsyncNoCryptoShare() {
            var modulus = 1009;
            var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var threshold = 2;
            var total = 3;

            var scheme = new AsyncNoCrypto(
                modulus, 
                marginalChanceDelayTargetRound: 4.DivRational(5), 
                marginalChanceAppendFakeRound: 9.DivRational(10));
            for (int secret = 0; secret < 5; secret++) {
                var shares = scheme.Create(secret, threshold, total, randomNumberGenerator);
                for (int s = threshold; s < total; s++) {
                    var combinedSecret = scheme.Combine(threshold, shares.Shuffle(randomNumberGenerator).Take(s).ToArray());
                    Assert.IsTrue(combinedSecret == secret);
                }
            }
        }
    }
}
