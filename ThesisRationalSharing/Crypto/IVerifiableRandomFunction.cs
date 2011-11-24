using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/** Generates secure and verifiable random numbers on demand. */
public interface IVerifiableRandomFunctionScheme<TPublicKey, TPrivateKey, TProof> {
    Tuple<TPublicKey, TPrivateKey> CreatePublicPrivateKeyPair(ISecureRandomNumberGenerator rng);
    ProofValue<TProof> Generate(TPrivateKey key, BigInteger input, BigInteger range);
    bool Verify(TPublicKey key, BigInteger input, BigInteger range, ProofValue<TProof> output);
}
public class ProofValue<TProof> {
    public readonly TProof Proof;
    public readonly BigInteger Value;
    public ProofValue(TProof proof, BigInteger value) {
        this.Proof = proof;
        this.Value = value;
    }
}
