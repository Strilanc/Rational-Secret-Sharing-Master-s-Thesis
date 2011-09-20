using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/** Generates secure random numbers on demand. */
[ContractClass(typeof(ContractClassForISecureRandomNumberGenerator))]
public interface ISecureRandomNumberGenerator {
    BigInteger GenerateNextValueMod(BigInteger strictCeiling);
}
[ContractClassFor(typeof(ISecureRandomNumberGenerator))]
public abstract class ContractClassForISecureRandomNumberGenerator : ISecureRandomNumberGenerator {
    public BigInteger GenerateNextValueMod(BigInteger strictCeiling) {
        Contract.Requires(strictCeiling > 0);
        Contract.Ensures(Contract.Result<BigInteger>() >= 0);
        Contract.Ensures(Contract.Result<BigInteger>() < strictCeiling);
        throw new NotImplementedException();
    }
}

/** A bit commitment.
 * Contains the information necessary to recognize a value, but not enough to learn the value. */
public interface ICommitment {
    bool Matches(BigInteger value);
}

/** A public key crypto sytem.
 * Anyone with the public key can decrypt messages, but only the holder of the private key can encrypt messages. */
public interface IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncrypted> {
    Tuple<TPublicKey, TPrivateKey> GenerateKeyPair(ISecureRandomNumberGenerator rng);
    TEncrypted PrivateEncrypt(TPrivateKey privateKey, BigInteger plain);
    BigInteger PublicDecrypt(TPublicKey publicKey, TEncrypted cipher);
}

public interface IReversibleMixingScheme<TValue, TKey> : IMixingScheme<TValue, TKey> {
    TValue Unmix(TValue mixed, TKey key);
}
public interface IMixingScheme<TValue, TKey> {
    TValue Mix(TValue value, TKey key);
}

/** A secret sharing scheme.
 * Splits a secret into parts, with some minimum number required to reconstruct the secret. */
[ContractClass(typeof(ContractClassForISharingScheme<>))]
public interface ISharingScheme<TShare> {
    BigInteger Combine(int degree, IList<TShare> shares);
    BigInteger? TryCombine(int degree, IList<TShare> shares);
    TShare[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng);
}
[ContractClassFor(typeof(ISharingScheme<>))]
public abstract class ContractClassForISharingScheme<TShare> : ISharingScheme<TShare> {
    public BigInteger Combine(int degree, IList<TShare> shares) {
        Contract.Requires(degree > 0);
        Contract.Requires(shares.Count >= degree);
        throw new NotImplementedException();
    }

    public BigInteger? TryCombine(int degree, IList<TShare> shares) {
        Contract.Requires(degree > 0);
        throw new NotImplementedException();
    }

    public TShare[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng) {
        Contract.Requires(secret >= 0);
        Contract.Requires(threshold >= 0);
        Contract.Requires(total >= threshold);
        Contract.Requires(rng != null);
        throw new NotImplementedException();
    }
}
