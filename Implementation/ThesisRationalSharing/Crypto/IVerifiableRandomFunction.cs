using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

/// <summary>Generates secure and verifiable random numbers on demand.</summary>
public interface IVerifiableRandomFunctionScheme<TPublicKey, TPrivateKey, TProof, TValue> {
    Tuple<TPublicKey, TPrivateKey> CreatePublicPrivateKeyPair(ISecureRandomNumberGenerator rng);
    ProofValue<TProof, TValue> Generate(TPrivateKey key, BigInteger input);
    bool Verify(TPublicKey key, BigInteger input, ProofValue<TProof, TValue> output);
    ProofValue<TProof, TValue> RandomMaliciousValue(ISecureRandomNumberGenerator rng);
}

[DebuggerDisplay("{ToString()}")]
public class ProofValue<TProof, TValue> {
    public readonly TProof Proof;
    public readonly TValue Value;
    public ProofValue(TProof proof, TValue value) {
        this.Proof = proof;
        this.Value = value;
    }
    public override string ToString() {
        return "Value=" + Value + ", Proof=" + Proof;
    }
}
