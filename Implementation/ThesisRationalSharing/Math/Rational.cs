using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;

///<summary>An arbitrary-precision rational number.</summary>
[DebuggerDisplay("{ToString()}")]
public struct Rational : IEquatable<Rational>, IComparable<Rational> {
    private readonly BigInteger _numerator;
    private readonly BigInteger _denominator;

    public static readonly Rational Zero = 0;
    public static readonly Rational One = 1;

    [ContractInvariantMethod()] private void ObjectInvariant() {
        Contract.Invariant(_denominator > 0);
        //Contract.Invariant(BigInteger.GreatestCommonDivisor(_numerator, _denominator) = 1) //GCD needs to be marked pure
    }

    /// <summary>Trivial constructor.</summary>
    public Rational(BigInteger numerator, BigInteger denominator) {
        Contract.Requires(denominator > 0);
        Contract.Requires(BigInteger.GreatestCommonDivisor(numerator, denominator) == 1); //GCD needs to be marked pure
        this._numerator = numerator;
        this._denominator = denominator;
    }

    /// <summary>
    /// returns the Rational equal to the given fraction (numerator/denominator).
    /// Normalizes by cancelling common factors and moving negative signs to the numerator.
    /// </summary>
    [Pure()]
    public static Rational FromFraction(BigInteger numerator, BigInteger denominator) {
        Contract.Requires(denominator != 0);
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (gcd.Sign != denominator.Sign) gcd = -gcd;
        var n = numerator / gcd;
        var d = denominator / gcd;
        Contract.Assume(d > 0);
        Contract.Assume(BigInteger.GreatestCommonDivisor(n, d) == 1);
        return new Rational(n, d);
    }

    public BigInteger Numerator { get { return _numerator; } }
    public BigInteger Denominator {
        get {
            Contract.Ensures(Contract.Result<BigInteger>() > 0);
            var r = _denominator == 0 ? 1 : _denominator;
            Contract.Assume(r > 0);
            return r;
        }
    }

    public int CompareTo(Rational other) {
        return (this.Numerator * other.Denominator - other.Numerator * this.Denominator).Sign;
    }
    public bool Equals(Rational other) {
        return this.Numerator == other.Numerator && this.Denominator == other.Denominator;
    }
    public override bool Equals(Object obj) {
        return obj is Rational && this.Equals((Rational)obj);
    }
    public override int GetHashCode() {
        unchecked {
            return this.Numerator.GetHashCode() ^ (this.Denominator.GetHashCode() * 3);
        }
    }
    public override String ToString() {
        if (Denominator == 1) return Numerator.ToString();
        return String.Format("{0}/{1}", Numerator, Denominator);
    }

    public static Rational operator +(Rational value1, Rational value2) {
        Contract.Assume(value1.Denominator * value2.Denominator != 0);
        return FromFraction(value1.Numerator * value2.Denominator + value2.Numerator * value1.Denominator,
                            value1.Denominator * value2.Denominator);
    }
    public static Rational operator *(Rational value1, Rational value2) {
        Contract.Assume(value1.Denominator * value2.Denominator != 0);
        return FromFraction(value1.Numerator * value2.Numerator,
                            value1.Denominator * value2.Denominator);
    }
    public static Rational operator /(Rational value1, Rational value2) {
        Contract.Requires(value2 != 0);
        Contract.Assume(value1.Denominator * value2.Numerator != 0);
        return FromFraction(value1.Numerator * value2.Denominator,
                            value1.Denominator * value2.Numerator);
    }
    public static Rational operator -(Rational value) {
        Contract.Assume(value.Denominator != 0);
        return new Rational(-value.Numerator, value.Denominator);
    }
    public static Rational operator %(Rational value1, Rational value2) {
        Contract.Assume(value1.Denominator * value2.Denominator != 0);
        return FromFraction((value1.Numerator * value2.Denominator) % (value2.Numerator * value1.Denominator),
                            value1.Denominator * value2.Denominator);
    }
    public static Rational operator -(Rational value1, Rational value2) {
        return value1 + -value2;
    }
    public Rational RaisedTo(int power) {
        if (power == Int32.MinValue) throw new ArgumentOutOfRangeException("power");
        if (power == 0) return 1;

        var np = BigInteger.Pow(this.Numerator, Math.Abs(power));
        var dp = BigInteger.Pow(this.Denominator, Math.Abs(power));
        if (power < 0) {
            if (np == 0) throw new DivideByZeroException();
            Contract.Assume(np != 0);
            return FromFraction(dp, np);
        }
        Contract.Assume(dp > 0);
        return new Rational(np, dp);
    }

    public static bool operator ==(Rational value1, Rational value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(Rational value1, Rational value2) {
        return !value1.Equals(value2);
    }
    public static bool operator >(Rational value1, Rational value2) {
        return value1.CompareTo(value2) > 0;
    }
    public static bool operator >=(Rational value1, Rational value2) {
        return value1.CompareTo(value2) >= 0;
    }
    public static bool operator <=(Rational value1, Rational value2) {
        return value1.CompareTo(value2) <= 0;
    }
    public static bool operator <(Rational value1, Rational value2) {
        return value1.CompareTo(value2) < 0;
    }

    public static implicit operator Rational(byte value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(sbyte value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(Int16 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(UInt16 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(Int32 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(UInt32 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(Int64 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(UInt64 value) {
        return new Rational(value, 1);
    }
    public static implicit operator Rational(BigInteger value) {
        return new Rational(value, 1);
    }
}
public static class RationalExtensions {
    ///<summary>Determines the integral result of rounding the rational towards 0.</summary>
    [Pure()]
    public static BigInteger Truncate(this Rational value) {
        return value.Numerator / value.Denominator; //BigInteger division truncates
    }
    ///<summary>Determines the integral result of rounding the rational towards negative infinity.</summary>
    [Pure()]
    public static BigInteger Floor(this Rational value) {
        var r = value.Numerator / value.Denominator;
        if (r > value) r -= 1;
        return r;
    }
    ///<summary>Determines the integral result of rounding the rational towards positive infinity.</summary>
    [Pure()]
    public static BigInteger Ceiling(this Rational value) {
        var r = value.Numerator / value.Denominator;
        if (r < value) r += 1;
        return r;
    }

    ///<summary>Gets a number that indicates the sign (negative, positive, or zero) of the rational.</summary>
    [Pure()]
    public static Int32 Sign(this Rational value) {
        return value.Numerator.Sign;
    }
    ///<summary>returns the absolute value of the rational.</summary>
    [Pure()]
    public static Rational Abs(this Rational value) {
        Contract.Ensures(Contract.Result<Rational>() >= 0);
        var r = Sign(value) < 0 ? -value : value;
        Contract.Assume(r >= 0);
        return r;
    }

    [Pure()]
    public static Rational DivRational(this BigInteger numerator, BigInteger denominator) {
        Contract.Requires(denominator != 0);
        return Rational.FromFraction(numerator, denominator);
    }
    [Pure()]
    public static Rational DivRational(this Int32 numerator, BigInteger denominator) {
        Contract.Requires(denominator != 0);
        return Rational.FromFraction(numerator, denominator);
    }
}
