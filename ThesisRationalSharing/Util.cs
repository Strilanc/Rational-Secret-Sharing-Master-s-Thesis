using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public static class Util {
    public static IEnumerable<T> Rotate<T>(this IEnumerable<T> items, int offset) {
        return items.ToArray().Rotate(offset);
    }
    public static int ProperMod(this int value, int divisor) {
        Contract.Requires(divisor > 0);
        if (value < 0) return ProperMod(value % divisor + divisor, divisor);
        return value % divisor;
    }
    public static BigInteger ProperMod(this BigInteger value, BigInteger divisor) {
        Contract.Requires(divisor > 0);
        if (value < 0) return ProperMod(value % divisor + divisor, divisor);
        return value % divisor;
    }
    public static T[] Rotate<T>(this IList<T> items, int offset) {
        offset %= items.Count;
        return Enumerable.Range(0, items.Count)
                         .Select(i => items[(i - offset).ProperMod(items.Count)])
                         .ToArray();
    }
    public static T[] Shuffle<T>(this IEnumerable<T> items, ISecureRandomNumberGenerator rng) {
        var buffer = items.ToArray();
        for (int i = 0; i < buffer.Length - 1; i++) {
            var j = (int)rng.GenerateNextValueMod(buffer.Length - i) + i;
            var t = buffer[i];
            buffer[i] = buffer[j];
            buffer[j] = t;
        }
        return buffer;
    }
    public static BigInteger GenerateNextValuePoisson(this ISecureRandomNumberGenerator rng, BigInteger chanceContinueNumerator, BigInteger chanceContinueDenominator) {
        BigInteger result = 0;
        while (rng.GenerateNextValueMod(chanceContinueDenominator) < chanceContinueNumerator)
            result += 1;
        return result;
    }
}

public class ShamirMixer : IReversibleMixingScheme<ShamirSecretSharing.Share, BigInteger> {
    public ShamirSecretSharing.Share Mix(ShamirSecretSharing.Share share, BigInteger offset) {
        return new ShamirSecretSharing.Share(share.X, share.Y + offset);
    }
    public ShamirSecretSharing.Share Unmix(ShamirSecretSharing.Share mixedShare, BigInteger offset) {
        return new ShamirSecretSharing.Share(mixedShare.X, mixedShare.Y - offset);
    }
}
public class ModMixer : IMixingScheme<BigInteger, BigInteger> {
    public readonly BigInteger Modulus;
    public ModMixer(BigInteger modulus) {
        this.Modulus = modulus;
    }
    public BigInteger Mix(BigInteger value, BigInteger key) {
        return (value + key) % Modulus;
    }
}
