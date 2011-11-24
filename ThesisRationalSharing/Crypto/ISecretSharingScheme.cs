using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/** A secret sharing scheme.
 * Splits a secret into parts, with some minimum number required to reconstruct the secret. */
[ContractClass(typeof(ContractClassForISecretSharingScheme<>))]
public interface ISecretSharingScheme<TShare> {
    BigInteger Range { get; }
    BigInteger Combine(int degree, IList<TShare> shares);
    BigInteger? TryCombine(int degree, IList<TShare> shares);
    TShare[] Create(BigInteger secret, int threshold, int total, ISecureRandomNumberGenerator rng);    
}
[ContractClassFor(typeof(ISecretSharingScheme<>))]
public abstract class ContractClassForISecretSharingScheme<TShare> : ISecretSharingScheme<TShare> {
    public BigInteger Range { get { 
        Contract.Ensures(Contract.Result<BigInteger>() > 0); 
        throw new NotImplementedException(); 
    } }
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
        Contract.Requires(secret < Range);
        Contract.Requires(threshold >= 0);
        Contract.Requires(total >= threshold);
        Contract.Requires(rng != null);
        throw new NotImplementedException();
    }
}
