using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public static class Util {
    [Pure]
    public static bool None<T>(this IEnumerable<T> items) {
        return !items.Any();
    }
    [Pure]
    public static bool Many<T>(this IEnumerable<T> items) {
        Contract.Requires(items != null);
        using (var e = items.GetEnumerator())
            return e.MoveNext() && e.MoveNext();
    }
    [Pure]
    public static bool IsSingle<T>(this IEnumerable<T> items) {
        Contract.Requires(items != null);
        using (var e = items.GetEnumerator())
            return e.MoveNext() && !e.MoveNext();
    }
    [Pure]
    public static IEnumerable<T> Duplicates<T>(this IEnumerable<T> items) {
        Contract.Requires(items != null);
        var set = new HashSet<T>();
        foreach (var item in items)
            if (!set.Add(item))
                yield return item;
    }
    [Pure]
    public static IEnumerable<T> Rotate<T>(this IEnumerable<T> items, int offset) {
        return items.ToArray().Rotate(offset);
    }
    [Pure]
    public static int ProperMod(this int value, int divisor) {
        Contract.Requires(divisor > 0);
        if (value < 0) return ProperMod(value % divisor + divisor, divisor);
        return value % divisor;
    }
    [Pure]
    public static BigInteger ProperMod(this BigInteger value, BigInteger divisor) {
        Contract.Requires(divisor > 0);
        if (value < 0) return ProperMod(value % divisor + divisor, divisor);
        return value % divisor;
    }
    [Pure]
    public static T[] Rotate<T>(this IList<T> items, int offset) {
        offset %= items.Count;
        return Enumerable.Range(0, items.Count)
                         .Select(i => items[(i - offset).ProperMod(items.Count)])
                         .ToArray();
    }
    [Pure]
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
    public static bool GenerateNextChance(this ISecureRandomNumberGenerator rng, Rational probability) {
        Contract.Requires(rng != null);
        Contract.Requires(probability >= 0);
        Contract.Requires(probability <= 1);
        if (probability == 0) return false;
        return rng.GenerateNextValueMod(probability.Denominator) <= rng.GenerateNextValueMod(probability.Numerator);
    }
    public static BigInteger GenerateNextValuePoisson(this ISecureRandomNumberGenerator rng, Rational chanceContinue) {
        Contract.Requires(rng != null);
        BigInteger result = 0;
        while (rng.GenerateNextChance(chanceContinue))
            result += 1;
        return result;
    }
    public static ModIntPolynomial GenerateNextModIntPolynomial(this ISecureRandomNumberGenerator rng, BigInteger modulus, int degree, BigInteger? specifiedZero = null) {
        Contract.Requires(rng != null);
        Contract.Requires(modulus > 0);
        Contract.Requires(degree >= 0);
        Contract.Requires(specifiedZero == null || specifiedZero.Value < modulus);
        var coefficients = new BigInteger[degree + 1];
        for (int i = 0; i < degree; i++) {
            coefficients[i] = i == 0 && specifiedZero.HasValue ? specifiedZero.Value : rng.GenerateNextValueMod(modulus);
        }
        return ModIntPolynomial.From(coefficients, modulus);
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
