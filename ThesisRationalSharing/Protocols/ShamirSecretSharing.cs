using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class ShamirSecretSharing {
    public static IEnumerable<Point<F>> GenerateShares<F>(IFiniteField<F> field, F secret, int threshold, ISecureRandomNumberGenerator r) {
        var poly = r.GenerateNextPolynomialWithSpecifiedZero(field, threshold - 1, secret);
        for (var i = field.One; !field.IsZero(i); i = field.Add(i, field.One)) {
            yield return Point<F>.FromPoly(poly, i);
        }
    }
    public static Point<F>[] CreateShares<F>(IFiniteField<F> field, F secret, int threshold, int total, ISecureRandomNumberGenerator r) {
        return GenerateShares(field, secret, threshold, r).Take(total).ToArray();
    }
    public static F CombineShares<F>(IField<F> field, int degree, IList<Point<F>> shares) {
        var r = TryCombineShares(field, degree, shares);
        if (r == null) throw new ArgumentException("Inconsistent shares.");
        return r.Item1;
    }
    public static Tuple<F> TryCombineShares<F>(IField<F> field, int degree, IList<Point<F>> shares) {
        if (shares.Count < degree) return null;
        var poly = Polynomial<F>.FromInterpolation(field, shares.Take(degree));
        if (shares.Any(e => !poly.EvaluateAt(e.X).Equals(e.Y))) return null;
        return Tuple.Create(poly.EvaluateAt(field.Zero));
    }
}
