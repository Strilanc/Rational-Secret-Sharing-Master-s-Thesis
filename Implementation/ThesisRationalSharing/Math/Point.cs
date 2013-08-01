using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Diagnostics;

///<summary>Points of modular integers.</summary>
[DebuggerDisplay("{ToString()}")]
public class Point<T> : IEquatable<Point<T>> {
    public readonly IField<T> Field;
    public readonly T X;
    public readonly T Y;

    public Point(IField<T> field, T x, T y) {
        Contract.Requires(field != null);
        Contract.Requires(x != null);
        Contract.Requires(y != null);
        this.Field = field;
        this.X = x;
        this.Y = y;
    }

    public override int GetHashCode() {
        unchecked {
            return Field.GetHashCode() ^ (X.GetHashCode() * 7) ^ (Y.GetHashCode() * 3);
        }
    }
    public override string ToString() {
        return String.Format(
            "({0}, {1}){2}", 
            Field.ListItemToString(X),
            Field.ListItemToString(Y), 
            Field.ListToStringSuffix);
    }
    public override bool Equals(object obj) {
        return this.Equals(obj as Point<T>);
    }
    public bool Equals(Point<T> other) {
        return Object.Equals(this.Field, other.Field) 
            && Object.Equals(this.X, other.X) 
            && object.Equals(this.Y, other.Y);
    }
    public static bool operator ==(Point<T> value1, Point<T> value2) {
        return Object.Equals(value1, value2);
    }
    public static bool operator !=(Point<T> value1, Point<T> value2) {
        return !Object.Equals(value1, value2);
    }

    public static Point<T> FromPoly(Polynomial<T> poly, T x) {
        return new Point<T>(poly.Field, x, poly.EvaluateAt(x));
    }
}
