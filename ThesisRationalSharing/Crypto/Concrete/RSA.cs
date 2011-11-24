using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

///<remarks>Example implementation only. Security vulnerabilities are present.</remarks>
[DebuggerDisplay("{ToString()}")]
public class RSA : IPublicKeyCryptoScheme<RSA.Key, RSA.Key, BigInteger>, IVerifiableRandomFunctionScheme<RSA.Key, RSA.Key, BigInteger> {
    public readonly BigInteger P;
    public readonly BigInteger Q;

    public RSA(BigInteger p, BigInteger q) {
        Contract.Requires(p > 1);
        Contract.Requires(q > 1);
        this.P = p;
        this.Q = q;
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

    public Tuple<Key, Key> GeneratePublicPrivateKeyPair(ISecureRandomNumberGenerator rng) {
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
        var d = new ModInt(e, t).MultiplicativeInverse().Value;
        var pub = new Key(n, e);
        var priv = new Key(n, d);
        return Tuple.Create(pub, priv);
    }

    public BigInteger PrivateEncrypt(Key privateKey, BigInteger plain) {
        return privateKey.Process(plain);
    }

    public BigInteger PublicDecrypt(Key publicKey, BigInteger cipher) {
        return publicKey.Process(cipher);
    }
    public override string ToString() {
        return "RSA: P = " + P + ", Q = " + Q;
    }

    public Tuple<RSA.Key, RSA.Key> CreatePublicPrivateKeyPair(ISecureRandomNumberGenerator rng) {
        return GeneratePublicPrivateKeyPair(rng);
    }

    public ProofValue<BigInteger> Generate(RSA.Key key, BigInteger input, BigInteger range) {
        var r = key.Process(input);
        return new ProofValue<BigInteger>(r, r % range);
    }

    public bool Verify(RSA.Key key, BigInteger input, BigInteger range, ProofValue<BigInteger> output) {
        return input == key.Process(output.Proof) && output.Value == output.Proof % range;
    }
}
