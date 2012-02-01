using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Points of modular integers.</summary>
[DebuggerDisplay("{ToString()}")]
public struct Point<T> : IEquatable<Point<T>> where T : IEquatable<T>, IField<T> {
    public readonly T X;
    public readonly T Y;
    private bool IsZero { get { return X == null; } }

    public Point(T x, T y) {
        this.X = x;
        this.Y = y;
    }

    public override int GetHashCode() {
        unchecked {
            return X.GetHashCode() ^ (Y.GetHashCode() * 3);
        }
    }
    public override string ToString() {
        if (this.IsZero) return "(0, 0)";
        return String.Format("({0}, {1}){2}", X.SequenceRepresentationItem, Y.SequenceRepresentationItem, X.SequenceRepresentationSuffix);
    }
    public override bool Equals(object obj) {
        return obj is Point<T> && this.Equals((Point<T>)obj);
    }
    public bool Equals(Point<T> other) {
        if (this.IsZero) return other.IsZero;
        if (other.IsZero) return false;
        return this.X.Equals(other.X) && this.Y.Equals(other.Y);
    }
    public static bool operator ==(Point<T> value1, Point<T> value2) {
        return value1.Equals(value2);
    }
    public static bool operator !=(Point<T> value1, Point<T> value2) {
        return !value1.Equals(value2);
    }

    public static Point<T> FromPoly(Polynomial<T> poly, T x) {
        return new Point<T>(x, poly.EvaluateAt(x));
    }
}
