using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

///<summary>Generates computationally secure random numbers on demand.</summary>
[ContractClass(typeof(ContractClassForISecureRandomNumberGenerator))]
public interface ISecureRandomNumberGenerator {
    ///<summary>Generates a uniformly distributed value in the range [0, strictCeiling).</summary>
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
