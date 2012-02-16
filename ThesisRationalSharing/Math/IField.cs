using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

///<summary>A mathematical field.</summary>
public interface IField<T> {
    [Pure]
    T Add(T value1, T value2);
    [Pure]
    T Multiply(T value1, T value2);
    [Pure]
    T AdditiveInverse(T value);
    [Pure]
    T MultiplicativeInverse(T value);

    /// <summary>The additive identity of the field.</summary>
    [Pure]
    T Zero { get; }
    [Pure]
    bool IsZero(T value);
    /// <summary>The multiplicative identity of the field.</summary>
    [Pure]
    T One { get; }
    [Pure]
    bool IsOne(T value);

    [Pure]
    String ListItemToString(T value);
    [Pure]
    String ListToStringSuffix { get; }
}
///<summary>A mathematical field of finite size.</summary>
public interface IFiniteField<T> : IField<T> {
    [Pure]
    BigInteger ToInt(T value);
    T Random(ISecureRandomNumberGenerator rng);
    [Pure]
    T FromInt(BigInteger i);
    [Pure]
    BigInteger Size { get; }
}
public static class FieldExtension {
    [Pure]
    public static T Subtract<T>(this IField<T> field, T lhs, T rhs) {
        Contract.Requires(field != null);
        return field.Add(lhs, field.AdditiveInverse(rhs));
    }
    [Pure]
    public static T Divide<T>(this IField<T> field, T value, T divisor) {
        Contract.Requires(field != null);
        Contract.Requires(!field.IsZero(divisor));
        return field.Multiply(value, field.MultiplicativeInverse(divisor));
    }
    [Pure]
    public static T Sum<T>(this IField<T> field, IEnumerable<T> sequence) {
        return sequence.Aggregate(field.Zero, (a, e) => field.Add(a, e));
    }
    [Pure]
    public static Polynomial<T> Sum<T>(this IField<T> field, IEnumerable<Polynomial<T>> sequence) {
        return sequence.Aggregate(Polynomial<T>.Zero(field), (a, e) => a + e);
    }
    [Pure]
    public static T Product<T>(this IField<T> field, IEnumerable<T> sequence) {
        return sequence.Aggregate(field.One, (a, e) => field.Multiply(a, e));
    }
    [Pure]
    public static Polynomial<T> Product<T>(this IField<T> field, IEnumerable<Polynomial<T>> sequence) {
        return sequence.Aggregate(Polynomial<T>.One(field), (a, e) => a * e);
    }
    [Pure]
    public static IEnumerable<T> Range<T>(this IField<T> field, T offset, int count) {
        while (count > 0) {
            yield return offset;
            offset = field.Add(offset, field.One);
            count -= 1;
        }
    }
    [Pure]
    public static Point<T> Point<T>(this IField<T> field, T x, T y) { 
        return new Point<T>(field, x, y); 
    }
    [Pure]
    public static Polynomial<T> InterpolatePolynomial<T>(this IField<T> field, IEnumerable<Point<T>> points) { 
        return Polynomial<T>.FromInterpolation(field, points); 
    }
}
