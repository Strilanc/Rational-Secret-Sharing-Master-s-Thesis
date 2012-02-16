using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;

[DebuggerDisplay("{ToString()}")]
public struct RationalField : IField<Rational> {
    public Rational Add(Rational value1, Rational value2) { return value1 + value2; }
    public Rational Multiply(Rational value1, Rational value2) { return value1 * value2; }
    public Rational AdditiveInverse(Rational value) { return -value; }
    public Rational MultiplicativeInverse(Rational value) { return 1 / value; }
    public string ListItemToString(Rational value) { return value.ToString(); }
    public bool IsZero(Rational value) { return value == 0; }
    public bool IsOne(Rational value) { return value == 1; }
    public Rational Zero { get { return 0; } }
    public Rational One { get {return 1; } }
    public string ListToStringSuffix { get { return ""; } }
    public override string ToString() { return "Rational Field"; }
}
