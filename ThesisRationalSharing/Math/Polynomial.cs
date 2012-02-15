using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

///<summary>A polynomial over a field.</summary>
[DebuggerDisplay("{ToString()}")]
public class Polynomial<T> : IEquatable<Polynomial<T>> {
    public readonly IField<T> Field;
    private readonly T[] _coefficients;
    
    public bool IsZero { get { return _coefficients.Length == 0; } }
    /// <summary>The polynomial's degree. Defaults to 0 for the zero polynomial.</summary>
    public int Degree { get { return Math.Max(0, _coefficients.Length - 1); } }
    /// <summary>The polynomial's coefficients in little-endian order.</summary>
    public IEnumerable<T> Coefficients { get { return _coefficients; } }

    /// <summary>Trivial constructor. Coefficients must not end in trailing zeroes.</summary>
    private Polynomial(IField<T> field, T[] normalizedCoefficients) {
        Contract.Requires(field != null);
        Contract.Requires(normalizedCoefficients != null);
        Contract.Requires(normalizedCoefficients.Length == 0 || !field.IsZero(normalizedCoefficients.Last()));
        this.Field = field;
        this._coefficients = normalizedCoefficients;
    }

    /// <summary>Creates a zero polynomial for the given field.</summary>
    [Pure]
    public static Polynomial<T> Zero(IField<T> field) {
        Contract.Requires(field != null);
        return new Polynomial<T>(field, new T[0]);
    }
    /// <summary>Creates a constant polynomial with value one for the given field.</summary>
    [Pure]
    public static Polynomial<T> One(IField<T> field) {
        Contract.Requires(field != null);
        return new Polynomial<T>(field, new T[] { field.One });
    }
    /// <summary>Creates a polynomial for the given field from the given sequence of coefficients in little-endian order.</summary>
    [Pure]
    public static Polynomial<T> FromCoefficients(IField<T> field, IEnumerable<T> coefficients) {
        Contract.Requires(field != null);
        Contract.Requires(coefficients != null);

        // cut trailing zeroes
        var coefs = coefficients.ToArray();
        int n = coefs.Length;
        if (n == 0) return Zero(field);
        while (n > 0 && field.IsZero(coefs[n - 1])) {
            n -= 1;
        }

        // copy remaining coefficients
        if (n == 0) return Zero(field);
        var r = new T[n];
        for (int i = 0; i < n; i++)
            r[i] = coefs[i];
        return new Polynomial<T>(field, r);
    }

    /// <summary>Evaluates the polynomial's y coordinate at the given x coordinate.</summary>
    [Pure]
    public T EvaluateAt(T x) {
        if (this.IsZero) return Field.Zero;
        var total = Field.Zero;
        var factor = Field.One;
        foreach (var c in _coefficients) {
            total = Field.Plus(total, Field.Times(c, factor));
            factor = Field.Times(factor, x);
        }
        return total;
    }

    public static Polynomial<T> operator -(Polynomial<T> value) {
        Contract.Requires(value != null);
        if (value.IsZero) return value;
        return new Polynomial<T>(value.Field, value._coefficients.Select(e => value.Field.AdditiveInverse(e)).ToArray());
    }
    public static Polynomial<T> operator +(Polynomial<T> value1, Polynomial<T> value2) {
        Contract.Requires(value1 != null);
        Contract.Requires(value2 != null);
        Contract.Requires(value1.Field.Equals(value2.Field));
        var field = value1.Field;
        if (value1.IsZero) return value2;
        if (value2.IsZero) return value1;
        return FromCoefficients(field,
            value1._coefficients.ZipPad(value2._coefficients, 
                (v1, v2) => value1.Field.Plus(v1, v2), 
                field.Zero, 
                field.Zero));
    }
    public static Polynomial<T> operator *(Polynomial<T> value, T factor) {
        Contract.Requires(value != null);
        if (value.IsZero) return value;
        return FromCoefficients(value.Field, value._coefficients.Select(e => value.Field.Times(e, factor)));
    }
    /// <summary>Returns the result of multiplying the polynomial by X to the shift'th power.</summary>
    public static Polynomial<T> operator <<(Polynomial<T> value, int shift) {
        Contract.Requires(value != null);
        Contract.Requires(shift >= 0);
        if (value.IsZero) return value;
        return FromCoefficients(value.Field, Enumerable.Repeat(value.Field.Zero, shift).Concat(value._coefficients));
    }

    public static Polynomial<T> operator -(Polynomial<T> value1, Polynomial<T> value2) {
        Contract.Requires(value1 != null);
        Contract.Requires(value2 != null);
        Contract.Requires(value1.Field.Equals(value2.Field));
        return value1 + -value2;
    }
    public static Polynomial<T> operator *(T factor, Polynomial<T> value) {
        return value * factor;
    }

    public static Polynomial<T> operator *(Polynomial<T> value1, Polynomial<T> value2) {
        Contract.Requires(value1 != null);
        Contract.Requires(value2 != null);
        Contract.Requires(value1.Field.Equals(value2.Field));
        if (value1.IsZero) return value1;
        if (value2.IsZero) return value2;
        var field = value1.Field;

        var coefs = Enumerable.Repeat(value1.Field.Zero, Math.Max(value1.Degree + value2.Degree, 0) + 1).ToArray();
        for (int i = 0; i <= value1.Degree; i++) {
            for (int j = 0; j <= value2.Degree; j++) {
                coefs[i + j] = field.Plus(coefs[i + j], field.Times(value1._coefficients[i], value2._coefficients[j]));
            }
        }

        return Polynomial<T>.FromCoefficients(field, coefs);
    }

    public static bool operator ==(Polynomial<T> value1, Polynomial<T> value2) {
        return Object.Equals(value1, value2);
    }
    public static bool operator !=(Polynomial<T> value1, Polynomial<T> value2) {
        return !Object.Equals(value1, value2);
    }

    public bool Equals(Polynomial<T> other) {
        if (other == null) return false;
        if (this.IsZero) return other.IsZero;
        if (other.IsZero) return false;
        return other._coefficients.SequenceEqual(this._coefficients);
    }
    public override bool Equals(object obj) {
        return this.Equals(obj as Polynomial<T>);
    }
    public override int GetHashCode() {
        return _coefficients.Aggregate(0, (a, e) => { unchecked { return (a * 3) ^ e.GetHashCode(); } });
    }

    private String RepresentCoefficient(T factor, int power) {
        if (Field.IsZero(factor)) return "0";
        if (power == 0) return Field.ListItemToString(factor);
        String x = "x" + (power == 1 ? "" : "^" + power);
        if (Field.IsOne(factor)) return x;
        return Field.ListItemToString(factor) + x;
    }
    public override string ToString() {
        if (this.IsZero) return "0";
        return String.Join(" + ", _coefficients.Select((e, i) => RepresentCoefficient(e, i)).Where(e => e != "0").Reverse()) + Field.ListToStringSuffix;
    }

    public static Polynomial<T> operator /(Polynomial<T> numerator, Polynomial<T> denominator) {
        Contract.Requires(numerator != null);
        Contract.Requires(denominator != null);
        Contract.Requires(denominator.Field.Equals(numerator.Field));
        Contract.Requires(!denominator.IsZero);
        Contract.Ensures(Contract.Result<Polynomial<T>>() * denominator == numerator);
        var r = numerator.DivRem(denominator);
        if (!r.Item2.IsZero) throw new ArgumentException("Division had remainder");
        return r.Item1;
    }
    public static Polynomial<T> operator %(Polynomial<T> numerator, Polynomial<T> denominator) {
        Contract.Requires(numerator != null);
        Contract.Requires(denominator != null);
        Contract.Requires(denominator.Field.Equals(numerator.Field));
        return numerator.DivRem(denominator).Item2;
    }
    public Tuple<Polynomial<T>, Polynomial<T>> DivRem(Polynomial<T> divisor) {
        Contract.Requires(divisor != null);
        Contract.Requires(!divisor.IsZero);
        Contract.Requires(this.Field.Equals(divisor.Field));
        Contract.Ensures(Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item1 * divisor + Contract.Result<Tuple<Polynomial<T>, Polynomial<T>>>().Item2 == this);
        if (this.IsZero) return Tuple.Create(this, this);
        var zero = this.Field.Zero;

        var quotientCoefs = Enumerable.Repeat(zero, Math.Max(0, this.Degree - divisor.Degree + 1)).ToArray();
        var remainderCoefs = this._coefficients.ToArray();
        
        var divisorInverseFactor = Field.MultiplicativeInverse(divisor._coefficients.Last());
        var normalizedDivisor = divisor * divisorInverseFactor;
        for (int i = remainderCoefs.Length - 1; i >= divisor.Degree; i--) {
            var c = remainderCoefs[i];
            if (Field.IsZero(c)) continue;
            //inlined: quotient += c * divisorInverseFactor << degreeDif
            quotientCoefs[i - divisor.Degree] = Field.Times(c, divisorInverseFactor);
            //inlined: remainder -= c * normalizedDivisor << degreeDif
            for (int j = 0; j <= divisor.Degree; j++) {
                remainderCoefs[i - j] = Field.Minus(remainderCoefs[i - j], Field.Times(c, normalizedDivisor._coefficients[normalizedDivisor._coefficients.Length - j - 1]));
            }
        }
        return Tuple.Create(FromCoefficients(Field, quotientCoefs), FromCoefficients(Field, remainderCoefs));
    }

    ///<summary>Returns the polynomial of minimum degree passing through all of the given coordinates.</summary>
    public static Polynomial<T> FromInterpolation(IField<T> field, IEnumerable<Point<T>> coords) {
        Contract.Requires(field != null);
        Contract.Requires(coords != null);
        Contract.Requires(coords.Select(e => e.X).Duplicates().None());
        Contract.Ensures(coords.All(e => Contract.Result<Polynomial<T>>().EvaluateAt(e.X).Equals(e.Y)));

        var pts = coords.ToArray();
        if (pts.Length == 0) return Zero(field);
        if (pts.Length == 1) return FromCoefficients(field, new T[] { coords.Single().Y });

        var U = FromCoefficients(field, new[] { field.One });
        var X = U << 1;

        var fullNumerator = pts.Select(e => X - U * e.X).Product(field);
        return pts.Select(e => {
            var numerator = fullNumerator / (X - U * e.X);
            var denominator = pts.Except(new[] { e }).Select(f => field.Minus(e.X, f.X)).Product(field);
            return e.Y * numerator * field.MultiplicativeInverse(denominator);
        }).Sum(field);
    }
}
