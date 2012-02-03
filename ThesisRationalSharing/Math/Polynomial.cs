using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>A polynomial over a field.</summary>
[DebuggerDisplay("{ToString()}")]
public struct Polynomial<T> : IEquatable<Polynomial<T>> where T : IField<T>, IEquatable<T> {
    private readonly T[] _coefficients;
    public bool IsZero { get { return _coefficients == null; } }
    /// <summary>The polynomial's degree. Defaults to 0 for the zero polynomial.</summary>
    public int Degree { get { return IsZero ? 0 : _coefficients.Length - 1; } }
    /// <summary>The polynomial's coefficients in little-endian order.</summary>
    public IEnumerable<T> Coefficients { get { return _coefficients ?? new T[0]; } }

    private T Field {
        get {
            Contract.Requires(!this.IsZero);
            return _coefficients[0];
        }
    }

    /// <summary>
    /// Trivial constructor.
    /// Coefficients must be non-empty and not end in trailing zeroes.
    /// Use default value or constructor to get the zero polynomial.
    /// </summary>
    private Polynomial(T[] normalizedCoefficients) {
        Contract.Requires(!normalizedCoefficients.Last().IsZero);
        this._coefficients = normalizedCoefficients;
    }

    /// <summary>Creates a polynomial from the given sequence of coefficients in little-endian order.</summary>
    [Pure]
    public static Polynomial<T> FromCoefficients(IEnumerable<T> coefficients) {
        if (coefficients == null) return default(Polynomial<T>);

        // cut trailing zeroes
        var coefs = coefficients.ToArray();
        int n = coefs.Length;
        if (n == 0) return default(Polynomial<T>);
        while (n > 0 && coefs[n - 1].IsZero) {
            n -= 1;
        }

        // copy remaining coefficients
        if (n == 0) return default(Polynomial<T>);
        var r = new T[n];
        for (int i = 0; i < n; i++)
            r[i] = coefs[i];
        return new Polynomial<T>(r);
    }

    /// <summary>Evaluates the polynomial's y coordinate at the given x coordinate.</summary>
    [Pure]
    public T EvaluateAt(T x) {
        if (this.IsZero) return x.Zero;
        var total = this.Field.Zero;
        var factor = this.Field.One;
        foreach (var c in _coefficients) {
            total = total.Plus(c.Times(factor));
            factor = factor.Times(x);
        }
        return total;
    }

    public static Polynomial<T> operator -(Polynomial<T> value) {
        if (value.IsZero) return value;
        return new Polynomial<T>(value._coefficients.Select(e => e.AdditiveInverse).ToArray());
    }
    public static Polynomial<T> operator +(Polynomial<T> value1, Polynomial<T> value2) {
        if (value1.IsZero) return value2;
        if (value2.IsZero) return value1;
        var zero = value1.Field.Zero;
        return FromCoefficients(value1._coefficients.ZipPad(
            value2._coefficients, 
            (v1, v2) => v1.Plus(v2), 
            value1.Field.Zero, 
            value2.Field.Zero));
    }
    public static Polynomial<T> operator *(Polynomial<T> value, T factor) {
        if (value.IsZero) return value;
        return FromCoefficients(value._coefficients.Select(e => e.Times(factor)));
    }
    /// <summary>Returns the result of multiplying the polynomial by X to the shift'th power.</summary>
    public static Polynomial<T> operator <<(Polynomial<T> value, int shift) {
        Contract.Requires(shift >= 0);
        if (value.IsZero) return value;
        return FromCoefficients(Enumerable.Repeat(value.Field.Zero, shift).Concat(value._coefficients));
    }

    public static Polynomial<T> operator -(Polynomial<T> value1, Polynomial<T> value2) {
        return value1 + -value2;
    }
    public static Polynomial<T> operator *(T factor, Polynomial<T> value) {
        return value * factor;
    }

    public static Polynomial<T> operator *(Polynomial<T> value1, Polynomial<T> value2) {
        if (value1.IsZero) return value1;
        if (value2.IsZero) return value2;

        var coefs = Enumerable.Repeat(value1.Field.Zero, Math.Max(value1.Degree + value2.Degree, 0) + 1).ToArray();
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
        if (factor.IsZero) return "0";
        if (power == 0) return factor.ListItemToString;
        String x = "x" + (power == 1 ? "" : "^" + power);
        if (factor.IsOne) return x;
        return factor.ListItemToString + x;
    }
    public override string ToString() {
        if (this.IsZero) return "0";
        return String.Join(" + ", _coefficients.Select((e, i) => RepresentCoefficient(e, i)).Where(e => e != "0").Reverse()) + _coefficients.First().ListToStringSuffix;
    }

    public static Polynomial<T> operator /(Polynomial<T> numerator, Polynomial<T> denominator) {
        Contract.Ensures(Contract.Result<Polynomial<T>>() * denominator == numerator);
        var r = numerator.DivRem(denominator);
        if (!r.Item2.IsZero) throw new ArgumentException("Division had remainder");
        return r.Item1;
    }
    public static Polynomial<T> operator %(Polynomial<T> numerator, Polynomial<T> denominator) {
        return numerator.DivRem(denominator).Item2;
    }
    public Tuple<Polynomial<T>, Polynomial<T>> DivRem(Polynomial<T> divisor) {
        Contract.Requires(!divisor.IsZero);
        Contract.Ensures(Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item1 * divisor + Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item2 == this);
        if (this.IsZero) return Tuple.Create(this, this);
        var zero = this.Field.Zero;

        var quotientCoefs = Enumerable.Repeat(zero, Math.Max(0, this.Degree - divisor.Degree + 1)).ToArray();
        var remainderCoefs = this._coefficients.ToArray();
        
        var divisorInverseFactor = divisor._coefficients.Last().MultiplicativeInverse;
        var normalizedDivisor = divisor * divisorInverseFactor;
        for (int i = remainderCoefs.Length - 1; i >= divisor.Degree; i--) {
            var c = remainderCoefs[i];
            if (c.IsZero) continue;
            //inlined: quotient += c * divisorInverseFactor << degreeDif
            quotientCoefs[i - divisor.Degree] = c.Times(divisorInverseFactor);
            //inlined: remainder -= c * normalizedDivisor << degreeDif
            for (int j = 0; j <= divisor.Degree; j++) {
                remainderCoefs[i - j] = remainderCoefs[i - j].Minus(c.Times(normalizedDivisor._coefficients[normalizedDivisor._coefficients.Length - j - 1]));
            }
        }
        return Tuple.Create(FromCoefficients(quotientCoefs), FromCoefficients(remainderCoefs));
    }

    ///<summary>Returns the polynomial of minimum degree passing through all of the given coordinates.</summary>
    public static Polynomial<T> FromInterpolation(IEnumerable<Point<T>> coords) {
        Contract.Requires(coords != null);
        Contract.Requires(coords.Select(e => e.X).Duplicates().None());
        Contract.Ensures(coords.All(e => Contract.Result<Polynomial<T>>().EvaluateAt(e.X).Equals(e.Y)));

        var pts = coords.ToArray();
        if (pts.Length == 0) return default(Polynomial<T>);
        if (pts.Length == 1) return FromCoefficients(new T[] { coords.Single().Y });

        var fieldOne = pts.First().X.One;
        var U = FromCoefficients(new[] { fieldOne });
        var X = U << 1;

        var fullNumerator = pts.Select(e => X - U * e.X).Product();
        return pts.Select(e => {
            var numerator = fullNumerator / (X - U * e.X);
            var denominator = pts.Except(new[] { e }).Select(f => e.X.Minus(f.X)).Product();
            return e.Y * numerator * denominator.MultiplicativeInverse;
        }).Sum();
    }
}
