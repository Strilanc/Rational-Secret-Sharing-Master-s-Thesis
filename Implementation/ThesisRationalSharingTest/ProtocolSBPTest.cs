using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace ThesisRationalSharingTest {
    [TestClass()]
    public class SBPTest {
        [TestMethod()]
        public void TestSBP() {
            var field = new ModIntField(1009);
            var rng = new RNG_BlumBlumbShub(modulus: 997 * 991, seed: 4);
            var cs = new CommitSHA1Scheme();
            var vrf = new VRF_RSA(997, 991, field);
            var threshold = 6;
            var total = 10;
            var alpha = new Rational(1, 10);

            for (var secret = field.Zero; secret.Value < 25; secret += 1) {
                var scheme = new ThesisRationalSharing.Protocols.SBP<ModInt, VRF_RSA.Key, VRF_RSA.Key, BigInteger>(threshold, total, field, cs, vrf, alpha);
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
