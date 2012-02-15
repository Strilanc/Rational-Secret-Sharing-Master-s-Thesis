using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

///<summary>A member of a mathematical field, with access to some ideally-would-be-static methods.</summary>
public interface IField<T> {
    T Plus(T value1, T value2);
    T Times(T value1, T value2);
    T AdditiveInverse(T value);
    T MultiplicativeInverse(T value);
    String ListItemToString(T value);
    bool IsZero(T value);
    bool IsOne(T value);

    /// <summary>The additive identity of the field.</summary>
    T Zero { get; }
    /// <summary>The multiplicative identity of the field.</summary>
    T One { get; }
    String ListToStringSuffix { get; }
}
///<summary>A member of a mathematical finite field, with access to some ideally-would-be-static methods.</summary>
public interface IFiniteField<T> : IField<T> {
    BigInteger ToInt(T value);
    T Random(ISecureRandomNumberGenerator rng);
    T FromInt(BigInteger i);
    BigInteger FieldSize { get; }
}
public static class FieldExtension {
    public static T Minus<T>(this IField<T> field, T lhs, T rhs) {
        return field.Plus(lhs, field.AdditiveInverse(rhs));
    }
}
