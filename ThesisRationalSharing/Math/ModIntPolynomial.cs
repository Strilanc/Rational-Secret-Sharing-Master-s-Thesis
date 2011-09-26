using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Polynomials over modular integers with associated arithmetic.</summary>
[DebuggerDisplay("{ToString()}")]
public struct ModIntPolynomial : IEquatable<ModIntPolynomial> {
    public readonly BigInteger Modulus;
    private readonly BigInteger[] Coefficients;

    /** Creates a modular integer from the given reduced value and modulus. */
    public ModIntPolynomial(BigInteger[] coefficients, BigInteger modulus) {
        Contract.Requires(coefficients != null);
        Contract.Requires(coefficients.All(e => e >= 0));
        Contract.Requires(coefficients.All(e => e < modulus));
        Contract.Requires(!coefficients.Any() || coefficients.Last() != 0);
        Contract.Requires(modulus > 0);
        this.Modulus = modulus;
        this.Coefficients = coefficients;
    }

    [Pure]
    public ModInt EvaluateAt(BigInteger x) {
        return EvaluateAt(ModInt.From(x, Modulus));
    }
    [Pure]
    public ModInt EvaluateAt(ModInt x) {
        Contract.Requires(x.Modulus == this.Modulus);
        ModInt total = new ModInt(0, Modulus);
        ModInt factor = new ModInt(1, Modulus);
        for (int i = 0; i < Coefficients.Length; i++) {
            total += Coefficients[i] * factor;
            factor *= x;
        }
        return total;
    }

    /** Creates a modular polynomial from the given coefficients and modulus, reducing the coefficients if necessary. */
    [Pure]
    public static ModIntPolynomial From(IEnumerable<ModInt> coefficients, BigInteger modulus) {
        Contract.Requires(modulus > 0);
        Contract.Requires(coefficients.All(e => e.Modulus == modulus));
        return ModIntPolynomial.From(coefficients.Select(e => e.Value), modulus);
    }
    [Pure]
    public static ModIntPolynomial From(IEnumerable<BigInteger> coefficients, BigInteger modulus) {
        Contract.Requires(modulus > 0);
        return new ModIntPolynomial(
            coefficients.Reverse().Select(e => ModInt.From(e, modulus).Value).SkipWhile(e => e == 0).Reverse().ToArray(),
            modulus);
    }
    [Pure]
    public static ModIntPolynomial From(IEnumerable<int> coefficients, BigInteger modulus) {
        Contract.Requires(modulus > 0);
        return new ModIntPolynomial(
            coefficients.Reverse().Select(e => ModInt.From(e, modulus).Value).SkipWhile(e => e == 0).Reverse().ToArray(),
            modulus);
    }

    [Pure]
    private static IEnumerable<TOut> ZipPad<T1, T2, TOut>(IEnumerable<T1> sequence1, IEnumerable<T2> sequence2, Func<T1, T2, TOut> projection) {
        using (var e1 = sequence1.GetEnumerator()) {
            using (var e2 = sequence2.GetEnumerator()) {
                while (true) {
                    var b1 = e1.MoveNext();
                    var b2 = e2.MoveNext();
                    if (!b1 && !b2) break;
                    T1 item1 = b1 ? e1.Current : default(T1);
                    T2 item2 = b2 ? e2.Current : default(T2);
                    yield return projection(item1, item2);
                }
            }
        }
    }

    public static ModIntPolynomial operator -(ModIntPolynomial value) {
        return From(value.Coefficients.Select(e => -e), value.Modulus);
    }
    public static ModIntPolynomial operator +(ModIntPolynomial value1, ModIntPolynomial value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        return From(ZipPad(value1.Coefficients, value2.Coefficients, (v1, v2) => v1 + v2), value1.Modulus);
    }
    public static ModIntPolynomial operator -(ModIntPolynomial value1, ModIntPolynomial value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        return From(ZipPad(value1.Coefficients, value2.Coefficients, (v1, v2) => v1 - v2), value1.Modulus);
    }
    public static ModIntPolynomial operator *(ModIntPolynomial value, BigInteger factor) {
        return From(value.Coefficients.Select(e => e * factor), value.Modulus);
    }
    public static ModIntPolynomial operator *(BigInteger factor, ModIntPolynomial value) {
        return From(value.Coefficients.Select(e => e * factor), value.Modulus);
    }
    public static ModIntPolynomial operator *(ModIntPolynomial value, ModInt factor) {
        Contract.Requires(factor.Modulus == value.Modulus);
        return From(value.Coefficients.Select(e => e * factor), value.Modulus);
    }
    public static ModIntPolynomial operator *(ModInt factor, ModIntPolynomial value) {
        Contract.Requires(factor.Modulus == value.Modulus);
        return From(value.Coefficients.Select(e => e * factor), value.Modulus);
    }
    public static ModIntPolynomial operator <<(ModIntPolynomial value, int shift) {
        Contract.Requires(shift >= 0);
        return From(Enumerable.Repeat(BigInteger.Zero, shift).Concat(value.Coefficients), value.Modulus);
    }
    public int Degree { get { return Coefficients.Length - 1; } }
    public static ModIntPolynomial operator *(ModIntPolynomial value1, ModIntPolynomial value2) {
        Contract.Requires(value1.Modulus == value2.Modulus);
        
        var coefs = Enumerable.Repeat(new ModInt(0, value1.Modulus), Math.Max(value1.Degree + value2.Degree, 0) + 1).ToArray();
        for (int i = 0; i <= value1.Degree; i++) {
            for (int j = 0; j <= value2.Degree; j++) {
                coefs[i + j] += value1.Coefficients[i] * value2.Coefficients[j];
            }
        }

        return ModIntPolynomial.From(coefs, value1.Modulus);
    }

    public static bool operator ==(ModIntPolynomial value1, ModIntPolynomial value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(ModIntPolynomial value1, ModIntPolynomial value2) {
        return !value1.Equals(value2);
    }

    public bool Equals(ModIntPolynomial other) {
        return other.Modulus == this.Modulus 
            && other.Coefficients.SequenceEqual(this.Coefficients);
    }
    public override bool Equals(object obj) {
        return obj is ModIntPolynomial && this.Equals((ModIntPolynomial)obj);
    }
    public override int GetHashCode() {
        return Coefficients.Aggregate(Modulus.GetHashCode(), (a, e) => { unchecked { return (a * 3) ^ e.GetHashCode(); } } );
    }

    private static String RepresentCoefficient(BigInteger factor, int power) {
        if (factor == 0) return "0";
        if (factor < 0) return "-" + RepresentCoefficient(-factor, power);
        if (power == 0) return factor.ToString();
        String x = "x" + (power == 1 ? "" : "^" + power);
        if (factor == 1) return x;
        return factor + x;
    }
    public override string ToString() {
        if (Coefficients.Length == 0) return "0 (mod " + Modulus + ")";
        return String.Join(" + ", Coefficients.Select((e, i) => RepresentCoefficient(e, i)).Where(e => e != "0").Reverse()) + " (mod " + Modulus + ")";
    }

    private static ModIntPolynomial Sum(IEnumerable<ModIntPolynomial> sequence) {
        return sequence.Aggregate((a, e) => a + e);
    }
    private static ModInt Product(IEnumerable<ModInt> sequence) {
        return sequence.Aggregate((a, e) => a * e);
    }
    private static ModIntPolynomial Product(IEnumerable<ModIntPolynomial> sequence) {
        return sequence.Aggregate((a, e) => a * e);
    }

    public static ModIntPolynomial operator /(ModIntPolynomial numerator, ModIntPolynomial denominator) {
        Contract.Requires(numerator.Modulus == denominator.Modulus);
        Contract.Ensures(Contract.Result<ModIntPolynomial>() * denominator == numerator);
        var r = numerator.DivRem(denominator);
        if (r.Item2.Coefficients.Length > 0) throw new ArgumentException("Division had remainder");
        return r.Item1;
    }
    public Tuple<ModIntPolynomial, ModIntPolynomial> DivRem(ModIntPolynomial divisor) {
        Contract.Requires(this.Modulus == divisor.Modulus);
        Contract.Ensures(Contract.Result<Tuple<ModIntPolynomial, ModIntPolynomial>>().Item1 * divisor + Contract.Result<Tuple<ModIntPolynomial, ModIntPolynomial>>().Item2 == this);
        var quotientCoefs = new BigInteger[Math.Max(0, this.Degree - divisor.Degree + 1)];
        var remainderCoefs = this.Coefficients.ToArray();
        
        var divisorInverseFactor = new ModInt(divisor.Coefficients.Last(), Modulus).MultiplicativeInverse().Value;
        var normalizedDivisor = divisor * divisorInverseFactor;
        for (int i = remainderCoefs.Length - 1; i >= divisor.Degree; i--) {
            var c = remainderCoefs[i];
            if (c == 0) continue;
            //inlined: quotient += c * divisorInverseFactor << degreeDif
            quotientCoefs[i - divisor.Degree] = c * divisorInverseFactor;
            //inlined: remainder -= c * normalizedDivisor << degreeDif
            for (int j = 0; j <= divisor.Degree; j++) {
                remainderCoefs[i - j] -= c * normalizedDivisor.Coefficients[normalizedDivisor.Coefficients.Length - j - 1];
            }
        }
        return Tuple.Create(ModIntPolynomial.From(quotientCoefs, Modulus), ModIntPolynomial.From(remainderCoefs, Modulus));
    }

    public static ModIntPolynomial FromInterpolation(IEnumerable<Tuple<BigInteger, BigInteger>> coords, BigInteger modulus) {
        Contract.Requires(coords != null);
        Contract.Requires(coords.Select(e => e.Item1).Duplicates().None());
        Contract.Ensures(coords.All(e => Contract.Result<ModIntPolynomial>().EvaluateAt(e.Item1) == e.Item2));
        return FromInterpolation(coords.Select(e => Tuple.Create(ModInt.From(e.Item1, modulus), ModInt.From(e.Item2, modulus))).ToArray(), modulus);
    }
    public static ModIntPolynomial FromInterpolation(IEnumerable<Tuple<ModInt, ModInt>> coords, BigInteger modulus) {
        Contract.Requires(coords != null);
        Contract.Requires(coords.Select(e => e.Item1).Duplicates().None());
        Contract.Requires(coords.All(e => e.Item1.Modulus == modulus));
        Contract.Requires(coords.All(e => e.Item2.Modulus == modulus));
        Contract.Ensures(coords.All(e => Contract.Result<ModIntPolynomial>().EvaluateAt(e.Item1) == e.Item2));

        var U = From(new[] {BigInteger.One}, modulus);
        var X = U << 1;

        if (coords.Count() == 0) return U * 0;
        if (coords.Count() == 1) return U * coords.Single().Item2;

        var fullNumerator = Product(coords.Select(e => X - U * e.Item1));
        return Sum(coords.Select(e => {
            var numerator = fullNumerator / (X - U * e.Item1);
            var denominator = Product(coords.Except(new[] { e }).Select(f => e.Item1 - f.Item1));
            return e.Item2 * numerator * denominator.MultiplicativeInverse();
        }));
    }
}
