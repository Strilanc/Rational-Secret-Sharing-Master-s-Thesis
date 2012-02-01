using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public static class Util {
    [Pure]
    public static IEnumerable<T> DistinctBy<T, K>(this IEnumerable<T> items, Func<T, K> projection) {
        var h = new HashSet<K>();
        foreach (var item in items)
            if (h.Add(projection(item))) 
                yield return item;
    }
    public static BigInteger GenerateNextValuePrimeBelow(this ISecureRandomNumberGenerator rng, BigInteger ceiling) {
        while (true) {
            var p = rng.GenerateNextValueMod(ceiling);
            if (p.IsLikelyPrime(rng)) return p;
        }
    }
    [Pure]
    public static T MinBy<T, C>(this IEnumerable<T> values, Func<T, C> proj) where C : IComparable<C> {
        var f = values.First();
        foreach (var e in values.Skip(1))
            if (proj(e).CompareTo(proj(f)) < 0) f = e;
        return f;
    }
    [Pure]
    public static IEnumerable<BigInteger> PartialSums(this IEnumerable<BigInteger> values) {
        var t = BigInteger.Zero;
        foreach (var v in values) {
            t += v;
            yield return t;
        }
    }
    [Pure]
    public static bool IsLikelyPrime(this BigInteger n, ISecureRandomNumberGenerator rng) {
        //Special cases
        if (n <= 1) return false;
        if (n <= 3) return true;
        if (n.IsEven) return false;

        //Trial divisions to avoid using entropy
        for (int i = 5; i < 25; i += 2)
            if (n % i == 0) return false;

        //Miller-Rabin
        const int repetitions = 100;
        var d = n - 1;
        var s = 0;
        while (d.IsEven ) {
            d >>= 1;
            s += 1;
        }
        for (int i = 0; i < repetitions; i++) {
            var a = rng.GenerateNextValueMod(n - 4) + 2;
            var x = (a*d) % n;
            if (x == 1 || x == n - 1) continue;
            for (var r = 1; r < s; r++) {
                x *= x;
                x %= n;
                if (x == 1) return false;
                if (x == n - 1) break;
            }
        }
        return true;
    }

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
    public static Polynomial<T> GenerateNextPolynomialWithSpecifiedZero<T>(this ISecureRandomNumberGenerator rng, int degree, T valueAtZero) where T : IFiniteField<T>, IEquatable<T> {
        Contract.Requires(rng != null);
        Contract.Requires(degree >= 0);
        var coefficients = new T[degree + 1];
        coefficients[0] = valueAtZero;
        for (int i = 1; i <= degree; i++) {
            coefficients[i] = valueAtZero.Random(rng);
        }
        return Polynomial<T>.FromCoefficients(coefficients);
    }
    public static Polynomial<T> GenerateNextPolynomial<T>(this ISecureRandomNumberGenerator rng, T field, int degree) where T : IFiniteField<T>, IEquatable<T> {
        Contract.Requires(rng != null);
        Contract.Requires(degree >= 0);
        var coefficients = new T[degree + 1];
        for (int i = 0; i <= degree; i++) {
            coefficients[i] = field.Random(rng);
        }
        return Polynomial<T>.FromCoefficients(coefficients);
    }
    public static ModInt Sum(this IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a + e);
    }
    public static Polynomial<T> Sum<T>(this IEnumerable<Polynomial<T>> sequence) where T : IField<T>, IEquatable<T> {
        return sequence.Aggregate((a, e) => a + e);
    }
    public static T Product<T>(this IEnumerable<T> sequence) where T : IField<T>, IEquatable<T> {
        return sequence.Aggregate((a, e) => a.Times(e));
    }
    public static ModInt Product(this IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a * e);
    }
    public static Polynomial<T> Product<T>(this IEnumerable<Polynomial<T>> sequence) where T : IField<T>, IEquatable<T> {
        return sequence.Aggregate((a, e) => a * e);
    }

    ///<summary>Zips two sequences, padding the shorter sequence with default items until it matches the length of the longer sequence.</summary>
    [Pure]
    public static IEnumerable<TOut> ZipPad<T1, T2, TOut>(this IEnumerable<T1> sequence1, IEnumerable<T2> sequence2, Func<T1, T2, TOut> projection, T1 def1 = default(T1), T2 def2 = default(T2)) {
        Contract.Requires(sequence1 != null);
        Contract.Requires(sequence2 != null);
        Contract.Requires(projection != null);
        using (var e1 = sequence1.GetEnumerator()) {
            using (var e2 = sequence2.GetEnumerator()) {
                while (true) {
                    var b1 = e1.MoveNext();
                    var b2 = e2.MoveNext();
                    if (!b1 && !b2) break;
                    T1 item1 = b1 ? e1.Current : def1;
                    T2 item2 = b2 ? e2.Current : def2;
                    yield return projection(item1, item2);
                }
            }
        }
    }
}
