using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

///<remarks>Example implementation only. Security vulnerabilities are present.</remarks>
[DebuggerDisplay("{ToString()}")]
public class VRF_RSA : IVerifiableRandomFunctionScheme<VRF_RSA.Key, VRF_RSA.Key, BigInteger, ModInt> {
    public readonly BigInteger P;
    public readonly BigInteger Q;
    public readonly ModInt VRFValueField;

    public VRF_RSA(BigInteger p, BigInteger q, ModInt VRFValueField) {
        Contract.Requires(p > 1);
        Contract.Requires(q > 1);
        this.P = p;
        this.Q = q;
        this.VRFValueField = VRFValueField;
    }

    [DebuggerDisplay("{ToString()}")]
    public class Key {
        public readonly BigInteger Modulus;
        public readonly BigInteger Exponent;
        public Key(BigInteger modulus, BigInteger exponent) {
            this.Modulus = modulus;
            this.Exponent = exponent;
        }
        public BigInteger Process(BigInteger value) {
            Contract.Requires(value < Modulus);
            Contract.Requires(value >= 0);
            return BigInteger.ModPow(value, Exponent, Modulus);
        }
        public override string ToString() {
            return "Exponent = " + Exponent + ", Modulus = " + Modulus;
        }
    }

    public Tuple<Key, Key> CreatePublicPrivateKeyPair(ISecureRandomNumberGenerator rng) {
        BigInteger g = 3;
        BigInteger m = 993;
        BigInteger x = rng.GenerateNextValueMod(m - 1) + 1;
        BigInteger h = BigInteger.ModPow(g, x, m);
        var n = P * Q;
        var t = (P - 1) * (Q - 1);
        BigInteger e;
        do {
            e = rng.GenerateNextValueMod(t - 2) + 2;
        } while (BigInteger.GreatestCommonDivisor(e, t) != 1);
        var d = new ModInt(e, t).MultiplicativeInverse.Value;
        var pub = new Key(n, e);
        var priv = new Key(n, d);
        return Tuple.Create(pub, priv);
    }

    public ProofValue<BigInteger, ModInt> Generate(Key key, BigInteger input) {
        var r = key.Process(input);
        return new ProofValue<BigInteger, ModInt>(r, ModInt.From(r, VRFValueField.Modulus));
    }
    public bool Verify(Key key, BigInteger input, ProofValue<BigInteger, ModInt> output) {
        return input == key.Process(output.Proof) && output.Value.Value == output.Proof % output.Value.Modulus;
    }

    public override string ToString() {
        return "VRF for F = Z mod " + VRFValueField.Modulus + " using RSA with P = " + P + ", Q = " + Q;
    }

    public ProofValue<BigInteger, ModInt> RandomMaliciousValue(ISecureRandomNumberGenerator rng) {
        return new ProofValue<BigInteger, ModInt>(rng.GenerateNextValueMod(P * Q), new ModInt(rng.GenerateNextValueMod(VRFValueField.Modulus), VRFValueField.Modulus));
    }
}
