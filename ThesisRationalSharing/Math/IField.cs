using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

///<summary>A member of a mathematical field, with access to some ideally-would-be-static methods.</summary>
public interface IField<T> where T : IField<T>, IEquatable<T> {
    T Plus(T other);
    T Times(T other);
    T AdditiveInverse { get; }
    T MultiplicativeInverse { get; }
    String ListItemToString { get; }
    bool IsZero { get; }
    bool IsOne { get; }
    T PlusOne();

    //"static"
    /// <summary>The additive identity of the field.</summary>
    T Zero { get; }
    /// <summary>The multiplicative identity of the field.</summary>
    T One { get; }
    String ListToStringSuffix { get; }
}
///<summary>A member of a mathematical finite field, with access to some ideally-would-be-static methods.</summary>
public interface IFiniteField<T> : IField<T> where T : IField<T>, IEquatable<T> {
    BigInteger ToInt();

    //"static"
    T Random(ISecureRandomNumberGenerator rng);
    T FromInt(BigInteger i);
    BigInteger FieldSize { get; }
}
public static class FieldExtension {
    public static T Minus<T>(this T lhs, T rhs) where T : IField<T>, IEquatable<T> {
        return lhs.Plus(rhs.AdditiveInverse);
    }
}
