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
            var shareMixer = new ShamirMixer();
            var roundNonceMixer = new ModMixer(997 * 991);
            var degree = 2;
            var total = 3;

            var scheme = AsyncVerifiedProtocol.From(shamir, publicKeySystem, shareMixer, roundNonceMixer);
            for (int secret = 0; secret < 5; secret++) {
                var shares = scheme.Create(secret, degree, total, randomNumberGenerator);
                var combinedSecret = scheme.Combine(degree, shares);
                var players = shares.Select(e => scheme.MakeRationalPlayer(e)).ToArray();
                var x = players;

                var playerRunSecrets = AsyncNetwork<IPlayer, IActorPlayer<BigInteger>, BigInteger>.Run(1, players);

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
            var shareMixer = new ShamirMixer();
            var roundNonceMixer = new ModMixer(997 * 991);
            var degree = 4;
            var total = 10;
            var coalitionSize = 3;
            var runs = 50;

            var scheme = new AsyncVerifiedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>(shamir, publicKeySystem, shareMixer, roundNonceMixer);
            
            var winRates = new List<dynamic>();
            for (degree = 4; degree < 5; degree++) {
                for (total = 5; total < 10; total++) {
                    for (coalitionSize = 2; coalitionSize < 4; coalitionSize++) {
                        var coalitionWinCounts = 0;
                        var otherWinCounts = 0;

                        for (int secret = 0; secret < runs; secret++) {
                            var shares = scheme.Create(secret, degree, total, randomNumberGenerator).Shuffle(randomNumberGenerator);
                            var combinedSecret = scheme.Combine(degree, shares);
                            var independents = shares.Skip(coalitionSize).Select(e => scheme.MakeRationalPlayer(e)).ToArray();
                            var coalition = scheme.MakeRationalCoalition(shares.Take(coalitionSize));
                            var playerRunSecrets = AsyncNetwork<IPlayer, IActorPlayer<BigInteger>, BigInteger>.Run(1, coalition.GetPlayers().Concat(independents));

                            Assert.IsTrue(combinedSecret == secret);
                            Assert.IsTrue(playerRunSecrets.Values.All(e => e == secret));
                            foreach (var x in playerRunSecrets) {
                                if (x.Key is AsyncVerifiedProtocol<ShamirSecretSharing.Share, BigInteger, RSA.Key, RSA.Key>.RationalPlayer) {
                                    otherWinCounts += 1;
                                } else {
                                    coalitionWinCounts += 1;
                                }
                            }
                        }
                        var coalWinRate = coalitionWinCounts / ((double)runs * coalitionSize);
                        var indWinRate = otherWinCounts / ((double)runs * (total - coalitionSize));
                        winRates.Add(new { N = total, M = degree, C = coalitionSize, cr = coalWinRate, ir = indWinRate });
                    }
                }
            }

            String s = String.Join(Environment.NewLine, winRates.Select(e => String.Format("{0}\t{1}\t{2}\t{3:0.00}\t{4:0.00}", e.N, e.M, e.C, e.cr, e.ir)));
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
