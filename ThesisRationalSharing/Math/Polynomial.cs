using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Polynomials over modular integers with associated arithmetic.</summary>
[DebuggerDisplay("{ToString()}")]
public struct Polynomial<T> : IEquatable<Polynomial<T>> where T : IField<T>, IEquatable<T> {
    private readonly T[] _coefficients;

    public IEnumerable<T> GetCoefficients() {
        return _coefficients ?? new T[0];
    }

    /** Creates a modular integer from the given reduced value and modulus. */
    public Polynomial(T[] coefficients) {
        Contract.Requires(coefficients != null);
        Contract.Requires(coefficients.None() || !coefficients.Last().Equals(coefficients.Last().Zero));
        this._coefficients = coefficients.Length == 0 ? null : coefficients;
    }

    public bool IsZero { get { return _coefficients == null; } }
    [Pure]
    public T EvaluateAt(T x) {
        if (IsZero) return x.Zero;
        var total = x.Zero;
        var factor = x.One;
        foreach (var c in _coefficients) {
            total = total.Plus(c.Times(factor));
            factor = factor.Times(x);
        }
        return total;
    }

    /** Creates a modular polynomial from the given coefficients and modulus, reducing the coefficients if necessary. */
    [Pure]
    public static Polynomial<T> FromCoefficients(IEnumerable<T> coefficients) {
        return new Polynomial<T>(coefficients.Reverse().SkipWhile(e => e.Equals(e.Zero)).Reverse().ToArray());
    }

    [Pure]
    private static IEnumerable<TOut> ZipPad<T1, T2, TOut>(IEnumerable<T1> sequence1, IEnumerable<T2> sequence2, Func<T1, T2, TOut> projection, T1 def1 = default(T1), T2 def2 = default(T2)) {
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

    public static Polynomial<T> operator -(Polynomial<T> value) {
        if (value.IsZero) return value;
        return FromCoefficients(value._coefficients.Select(e => e.AdditiveInverse));
    }
    public static Polynomial<T> operator +(Polynomial<T> value1, Polynomial<T> value2) {
        if (value1.IsZero) return value2;
        if (value2.IsZero) return value1;
        var zero = value1._coefficients.First().Zero;
        return FromCoefficients(ZipPad(value1._coefficients, value2._coefficients, (v1, v2) => v1.Plus(v2), zero, zero));
    }
    public static Polynomial<T> operator -(Polynomial<T> value1, Polynomial<T> value2) {
        if (value1.IsZero) return -value2;
        if (value2.IsZero) return value1;
        var zero = value1._coefficients.First().Zero;
        return FromCoefficients(ZipPad(value1._coefficients, value2._coefficients, (v1, v2) => v1.Plus(v2.AdditiveInverse), zero, zero));
    }
    public static Polynomial<T> operator *(Polynomial<T> value, T factor) {
        if (value.IsZero) return value;
        return FromCoefficients(value._coefficients.Select(e => e.Times(factor)));
    }
    public static Polynomial<T> operator *(T factor, Polynomial<T> value) {
        return value * factor;
    }
    public static Polynomial<T> operator <<(Polynomial<T> value, int shift) {
        Contract.Requires(shift >= 0);
        if (value.IsZero) return value;
        var zero = value._coefficients.First().Zero;
        return FromCoefficients(Enumerable.Repeat(zero, shift).Concat(value._coefficients));
    }
    public int Degree { get { return _coefficients.Length - 1; } }
    public static Polynomial<T> operator *(Polynomial<T> value1, Polynomial<T> value2) {
        if (value1.IsZero) return value1;
        if (value2.IsZero) return value2;
        var zero = value1._coefficients.First().Zero;

        var coefs = Enumerable.Repeat(zero, Math.Max(value1.Degree + value2.Degree, 0) + 1).ToArray();
        for (int i = 0; i <= value1.Degree; i++) {
            for (int j = 0; j <= value2.Degree; j++) {
                coefs[i + j] = coefs[i + j].Plus(value1._coefficients[i].Times(value2._coefficients[j]));
            }
        }

        return Polynomial<T>.FromCoefficients(coefs);
    }

    public static bool operator ==(Polynomial<T> value1, Polynomial<T> value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(Polynomial<T> value1, Polynomial<T> value2) {
        return !value1.Equals(value2);
    }

    public bool Equals(Polynomial<T> other) {
        if (this.IsZero) return other.IsZero;
        if (other.IsZero) return false;
        return other._coefficients.SequenceEqual(this._coefficients);
    }
    public override bool Equals(object obj) {
        return obj is Polynomial<T> && this.Equals((Polynomial<T>)obj);
    }
    public override int GetHashCode() {
        if (this.IsZero) return 0;
        return _coefficients.Aggregate(0, (a, e) => { unchecked { return (a * 3) ^ e.GetHashCode(); } });
    }

    private static String RepresentCoefficient(T factor, int power) {
        if (factor.Equals(factor.Zero)) return "0";
        if (power == 0) return factor.SequenceRepresentationItem;
        String x = "x" + (power == 1 ? "" : "^" + power);
        if (factor.Equals(factor.One)) return x;
        return factor.SequenceRepresentationItem + x;
    }
    public override string ToString() {
        if (this.IsZero) return "0";
        return String.Join(" + ", _coefficients.Select((e, i) => RepresentCoefficient(e, i)).Where(e => e != "0").Reverse()) + _coefficients.First().SequenceRepresentationSuffix;
    }

    public static Polynomial<T> operator /(Polynomial<T> numerator, Polynomial<T> denominator) {
        Contract.Ensures(Contract.Result<Polynomial<T>>() * denominator == numerator);
        var r = numerator.DivRem(denominator);
        if (!r.Item2.IsZero) throw new ArgumentException("Division had remainder");
        return r.Item1;
    }
    public Tuple<Polynomial<T>, Polynomial<T>> DivRem(Polynomial<T> divisor) {
        Contract.Requires(!divisor.IsZero);
        Contract.Ensures(Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item1 * divisor + Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item2 == this);
        if (this.IsZero) return Tuple.Create(this, this);
        var zero = this._coefficients.First().Zero;

        var quotientCoefs = Enumerable.Repeat(zero, Math.Max(0, this.Degree - divisor.Degree + 1)).ToArray();
        var remainderCoefs = this._coefficients.ToArray();
        
        var divisorInverseFactor = divisor._coefficients.Last().MultiplicativeInverse;
        var normalizedDivisor = divisor * divisorInverseFactor;
        for (int i = remainderCoefs.Length - 1; i >= divisor.Degree; i--) {
            var c = remainderCoefs[i];
            if (c.Equals(c.Zero)) continue;
            //inlined: quotient += c * divisorInverseFactor << degreeDif
            quotientCoefs[i - divisor.Degree] = c.Times(divisorInverseFactor);
            //inlined: remainder -= c * normalizedDivisor << degreeDif
            for (int j = 0; j <= divisor.Degree; j++) {
                remainderCoefs[i - j] = remainderCoefs[i - j].Plus(c.Times(normalizedDivisor._coefficients[normalizedDivisor._coefficients.Length - j - 1]).AdditiveInverse);
            }
        }
        return Tuple.Create(FromCoefficients(quotientCoefs), FromCoefficients(remainderCoefs));
    }

    public static Polynomial<T> FromInterpolation(IEnumerable<Point<T>> coords) {
        Contract.Requires(coords != null);
        Contract.Requires(coords.Select(e => e.X).Duplicates().None());
        Contract.Ensures(coords.All(e => Contract.Result<Polynomial<T>>().EvaluateAt(e.X).Equals(e.Y)));

        if (coords.Count() == 0) return default(Polynomial<T>);
        if (coords.Count() == 1) return new Polynomial<T>(new T[] { coords.Single().Y });

        var U = FromCoefficients(new[] {coords.First().X.One});
        var X = U << 1;

        var fullNumerator = coords.Select(e => X - U * e.X).Product();
        return coords.Select(e => {
            var numerator = fullNumerator / (X - U * e.X);
            var denominator = coords.Except(new[] { e }).Select(f => e.X.Plus(f.X.AdditiveInverse)).Product();
            return e.Y * numerator * denominator.MultiplicativeInverse;
        }).Sum();
    }
}
