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
            total = Field.Add(total, Field.Multiply(c, factor));
            factor = Field.Multiply(factor, x);
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
                (v1, v2) => value1.Field.Add(v1, v2), 
                field.Zero, 
                field.Zero));
    }
    public static Polynomial<T> operator *(Polynomial<T> value, T factor) {
        Contract.Requires(value != null);
        if (value.IsZero) return value;
        return FromCoefficients(value.Field, value._coefficients.Select(e => value.Field.Multiply(e, factor)));
    }
    public static Polynomial<T> operator /(Polynomial<T> value, T factor) {
        Contract.Requires(value != null);
        Contract.Requires(!value.Field.IsZero(factor));
        if (value.IsZero) return value;
        return value * value.Field.MultiplicativeInverse(factor);
    }
    /// <summary>Returns the result of multiplying the polynomial by X to the shift'th power.</summary>
    public static Polynomial<T> operator <<(Polynomial<T> value, int shift) {
        Contract.Requires(value != null);
        Contract.Requires(shift >= 0);
        if (shift == 0) return value;
        if (value.IsZero) return value;
        return new Polynomial<T>(value.Field, Enumerable.Repeat(value.Field.Zero, shift).Concat(value._coefficients).ToArray());
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
                coefs[i + j] = field.Add(coefs[i + j], field.Multiply(value1._coefficients[i], value2._coefficients[j]));
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
        if (Field.IsZero(factor)) return null;
        if (power == 0) return Field.ListItemToString(factor);
        String x = "x" + (power == 1 ? "" : "^" + power);
        if (Field.IsOne(factor)) return x;
        return Field.ListItemToString(factor) + x;
    }
    public override string ToString() {
        if (this.IsZero) return Field.ListItemToString(Field.Zero) + Field.ListToStringSuffix;
        return String.Join(" + ", _coefficients.Select((e, i) => RepresentCoefficient(e, i)).Where(e => e != null).Reverse()) + Field.ListToStringSuffix;
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
            quotientCoefs[i - divisor.Degree] = Field.Multiply(c, divisorInverseFactor);
            //inlined: remainder -= c * normalizedDivisor << degreeDif
            for (int j = 0; j <= divisor.Degree; j++) {
                remainderCoefs[i - j] = Field.Subtract(remainderCoefs[i - j], Field.Multiply(c, normalizedDivisor._coefficients[normalizedDivisor._coefficients.Length - j - 1]));
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
        var U = One(field);
        var X = U << 1;
        var fullNumerator = field.Product(pts.Select(e => X - U * e.X));
        return field.Sum(pts.Select(e => {
            var numerator = fullNumerator / (X - U * e.X);
            var denominator = field.Product(pts.Except(new[] { e }).Select(f => field.Subtract(e.X, f.X)));
            return e.Y * numerator / denominator;
        }));
    }
}
