using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public static class Extensions {
    public static BigInteger GenerateNextValuePoisson(this ISecureRandomNumberGenerator rng, BigInteger chanceContinueNumerator, BigInteger chanceContinueDenominator) {
        BigInteger result = 0;
        while (rng.GenerateNextValueMod(chanceContinueDenominator) < chanceContinueNumerator)
            result += 1;
        return result;
    }
}
