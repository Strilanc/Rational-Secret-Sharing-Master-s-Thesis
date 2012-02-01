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
    String SequenceRepresentationItem { get; }

    //"static"
    T Zero { get; }
    T One { get; }
    String SequenceRepresentationSuffix { get; }
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
