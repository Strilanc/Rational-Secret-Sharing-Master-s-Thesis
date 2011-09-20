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
                         .Select(i => items[(items.Count + i - offset) % items.Count])
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
}
