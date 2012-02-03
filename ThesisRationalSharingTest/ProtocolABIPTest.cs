using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class ABIPTest {
        [TestMethod()]
        public void TestABIP() {
            var field = new ModInt(0, 1009);
            var rng = new RNG_BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var vrf = new VRF_RSA(997, 991, field);

            var rpa = Rational.Zero;
            var rps = Rational.Zero;
            var rca = Rational.Zero;
            var rcf = Rational.Zero;

            Action<ModInt, int, int, int, int, Rational> f = (ModInt secret, int threshold, int total, int numColluders, int numMalicious, Rational alpha) => {
                var scheme = new ThesisRationalSharing.Protocols.ABIP<ModInt, VRF_RSA.Key, VRF_RSA.Key, BigInteger>(threshold, total, field, vrf, alpha);
                var shares = scheme.Deal(secret, rng).Shuffle(rng);
                Assert.IsTrue(scheme.CoalitionCombine(shares) == secret);

                var colludingRationalPlayers = scheme.MakeCooperateUntilLearnCoalition(shares.Take(numColluders).ToArray());
                var maliciousPlayers = shares.Skip(numColluders).Take(numMalicious).Select(e => scheme.MakeSendNoMessagePlayer(e)).ToArray();
                var rationalPlayers = shares.Skip(numColluders + numMalicious).Select(e => scheme.MakeCooperateUntilLearnPlayer(e)).ToArray();
                scheme.RunProtocol(colludingRationalPlayers.Concat(maliciousPlayers.Concat(rationalPlayers)));

                var shouldPass = total - numMalicious >= threshold;
                var mayCatastrophe = total - numMalicious == threshold - 1;
                var mustPass = shouldPass && numColluders < 2;
                foreach (var m in colludingRationalPlayers) {
                    Assert.IsTrue(shouldPass == (m.RecoveredSecretValue != null));
                    Assert.IsTrue(m.RecoveredSecretValue == null || mayCatastrophe || m.RecoveredSecretValue.Item1 == secret);
                    if (shouldPass) Assert.IsTrue(m.RecoveredSecretValue.Item1 == secret);
                }
                foreach (var m in rationalPlayers) {
                    Assert.IsTrue(!mustPass || (m.RecoveredSecretValue != null && m.RecoveredSecretValue.Item1 == secret));
                    Assert.IsTrue(m.RecoveredSecretValue == null || mayCatastrophe || shouldPass);
                    Assert.IsTrue(m.RecoveredSecretValue == null || mayCatastrophe || m.RecoveredSecretValue.Item1 == secret);
                    if (shouldPass) {
                        rpa += 1;
                        if (m.RecoveredSecretValue != null) rps += 1;
                        Assert.IsTrue(m.RecoveredSecretValue == null || m.RecoveredSecretValue.Item1 == secret);
                    }
                    if (mayCatastrophe) {
                        rca += 1;
                        if (m.RecoveredSecretValue != null && m.RecoveredSecretValue.Item1 != secret) rcf += 1;
                    }
                }
            };
            for (var secret = field; secret.Value < 25; secret += 1) {
                var threshold = 6;
                var total = 9;
                var alpha = new Rational(1, 4);
                var numColluders = (int)rng.GenerateNextValueMod(threshold);
                var numMalicious = (int)rng.GenerateNextValueMod(total - numColluders);
                f(secret, threshold, total, numColluders, numMalicious, alpha);
            }
            for (int i = 0; i < 5; i++) {
                f(field - 1, 4, 6, 0, 3, Rational.One / 10);
            }

            //non-colluding rational players may be beat by the coalition
            Assert.IsTrue(rps / rpa > 0);
            Assert.IsTrue(rps / rpa < 1); 
            //catastrophe may happen
            Assert.IsTrue(rcf / rca > 0);
            Assert.IsTrue(rcf / rca < 1);
        }
    }
}
