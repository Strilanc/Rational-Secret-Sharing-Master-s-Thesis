using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class SUIPTest {
        [TestMethod()]
        public void TestSUIP() {
            var field = new ModInt(0, 1009);
            var rng = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var threshold = 6;
            var total = 10;
            var alpha = new Rational(1, 3);
            var gamma = new Rational(1, 5);
            var omega = 2;
            var beta = 2;

            for (var secret = field; secret.Value < 25; secret += 1) {
                var scheme = new ThesisRationalSharing.Protocols.SUIP<ModInt>(threshold, total, field, alpha, gamma, omega, beta);
                var shares = scheme.Deal(secret, rng).Shuffle(rng);
                Assert.IsTrue(scheme.CoalitionCombine(shares) == secret);

                var numMalicious = (int)rng.GenerateNextValueMod(total);
                
                var maliciousPlayers = shares.Take(numMalicious).Select(e => scheme.MakeSendNoMessagePlayer(e)).ToArray();
                var rationalPlayers = shares.Skip(numMalicious).Select(e => scheme.MakeCooperateUntilLearnPlayer(e)).ToArray();
                scheme.RunProtocol(maliciousPlayers.Concat(rationalPlayers));

                var shouldPass = total - numMalicious >= threshold;
                foreach (var m in rationalPlayers) {
                    Assert.IsTrue(shouldPass == (m.RecoveredSecretValue != null));
                    if (shouldPass) Assert.IsTrue(m.RecoveredSecretValue.Item1 == secret);
                }
            }
        }
    }
}
