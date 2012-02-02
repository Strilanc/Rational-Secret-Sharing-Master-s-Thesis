using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ABCPTest {
        [TestMethod()]
        public void TestABCP() {
            var field = new ModInt(0, 437597921); // large enough to make accidental discovery unlikely
            var rng = new RNG_BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var cs = new CommitSHA1Scheme();
            var vrf = new VRF_RSA(997, 991, field);
            var threshold = 6;
            var total = 10;
            var alpha = new Rational(1, 10);
            var delta = 1;

            for (var secret = field; secret.Value < 25; secret += 1) {
                var scheme = new ThesisRationalSharing.Protocols.ABCP<ModInt, VRF_RSA.Key, VRF_RSA.Key, BigInteger>(threshold, total, field, delta, cs, vrf, alpha);
                var shares = scheme.Deal(secret, rng).Shuffle(rng);
                Assert.IsTrue(scheme.CoalitionCombine(shares) == secret);

                var numMalicious = (int)rng.GenerateNextValueMod(total);

                var maliciousPlayers = shares.Take(numMalicious).Select(e => scheme.MakeSendNoMessagePlayer(e)).ToArray();
                var rationalPlayers = shares.Skip(numMalicious).Select(e => scheme.MakeCooperateUntilLearnPlayer(e)).ToArray();
                scheme.RunProtocol(maliciousPlayers.Concat(rationalPlayers));

                var shouldPass = total - numMalicious >= threshold;
                var passes = 0;
                foreach (var m in rationalPlayers) {
                    if (m.RecoveredSecretValue != null) {
                        Assert.IsTrue(m.RecoveredSecretValue.Item1 == secret);
                        Assert.IsTrue(shouldPass);
                        passes += 1;
                    }
                }
                Assert.IsTrue(!shouldPass || (passes <= total - numMalicious - delta && passes >= total - numMalicious - threshold));
            }
        }
        [TestMethod()]
        public void TestABCPCollusion() {
            var field = new ModInt(0, 437597921); // large enough to make accidental discovery unlikely
            var rng = new RNG_BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var cs = new CommitSHA1Scheme();
            var vrf = new VRF_RSA(997, 991, field);
            var threshold = 6;
            var total = 10;
            var alpha = new Rational(1, 10);
            var delta = 1;

            var passes = 0;
            var attempts = Rational.Zero;
            for (var secret = field; secret.Value < 5; secret += 1) {
                var scheme = new ThesisRationalSharing.Protocols.ABCP<ModInt, VRF_RSA.Key, VRF_RSA.Key, BigInteger>(threshold, total, field, delta, cs, vrf, alpha);
                var shares = scheme.Deal(secret, rng).Shuffle(rng);
                Assert.IsTrue(scheme.CoalitionCombine(shares) == secret);

                var numColluding = threshold - 1;
                var colluders = scheme.MakeCooperateUntilLearnCoalition(shares.Take(numColluding).ToArray());
                var rationalPlayers = shares.Skip(numColluding).Select(e => scheme.MakeCooperateUntilLearnPlayer(e)).ToArray();
                scheme.RunProtocol(colluders.Concat(rationalPlayers));

                foreach (var m in colluders) {
                    Assert.IsTrue(m.RecoveredSecretValue != null);
                    Assert.IsTrue(m.RecoveredSecretValue.Item1 == secret);
                }
                foreach (var m in rationalPlayers) {
                    attempts += 1;
                    if (m.RecoveredSecretValue != null) {
                        Assert.IsTrue(m.RecoveredSecretValue.Item1 == secret);
                        passes += 1;
                    }
                }
            }
            Assert.IsTrue(passes > 0 && passes < attempts);
        }
    }
}
