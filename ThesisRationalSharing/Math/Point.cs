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
    private readonly T _x;
    private readonly T _y;
    private bool IsDefaultConstructed { get { return _x == null; } }
    public T X { 
        get {
            if (IsDefaultConstructed) throw new InvalidOperationException("Default point with no elements");
            return _x; 
        } 
    }
    public T Y {
        get {
            if (IsDefaultConstructed) throw new InvalidOperationException("Default point with no elements");
            return _y;
        }
    }

    public Point(T x, T y) {
        Contract.Requires(x != null);
        Contract.Requires(y != null);
        this._x = x;
        this._y = y;
    }

    public override int GetHashCode() {
        unchecked {
            if (IsDefaultConstructed) return 0;
            return _x.GetHashCode() ^ (_y.GetHashCode() * 3);
        }
    }
    public override string ToString() {
        if (IsDefaultConstructed) return "(?0, ?0)";
        return String.Format(
            "({0}, {1}){2}", 
            X.ListItemToString, 
            Y.ListItemToString, 
            X.ListToStringSuffix);
    }
    public override bool Equals(object obj) {
        return obj is Point<T> && this.Equals((Point<T>)obj);
    }
    public bool Equals(Point<T> other) {
        if (this.IsDefaultConstructed || other.IsDefaultConstructed)
            return this.IsDefaultConstructed == other.IsDefaultConstructed;
        return this._x.Equals(other._x) && this._y.Equals(other._y);
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
