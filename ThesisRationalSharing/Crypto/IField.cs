using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public interface IField<T> where T : IField<T>, IEquatable<T> {
    T Zero { get; }
    T One { get; }
    T Plus(T other);
    T Times(T other);
    T AdditiveInverse { get; }
    T MultiplicativeInverse { get; }
    String SequenceRepresentationItem { get; }
    String SequenceRepresentationSuffix { get; }
}
public interface IFiniteField<T> : IField<T> where T : IField<T>, IEquatable<T> {
    T Random(ISecureRandomNumberGenerator rng);
    BigInteger ToInt();
    T FromInt(BigInteger i);
    BigInteger FieldSize { get; }
}
