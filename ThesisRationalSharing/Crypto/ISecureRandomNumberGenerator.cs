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
