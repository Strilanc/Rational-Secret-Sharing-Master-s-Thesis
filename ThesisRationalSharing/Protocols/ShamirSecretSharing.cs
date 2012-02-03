using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public class ShamirSecretSharing<F> where F : IFiniteField<F>, IEquatable<F> {
    public static IEnumerable<Point<F>> GenerateShares(F secret, int threshold, ISecureRandomNumberGenerator r) {
        var fieldOne = secret.One;

        var poly = r.GenerateNextPolynomialWithSpecifiedZero(threshold - 1, secret);
        for (var i = fieldOne; !i.IsZero; i = i.PlusOne()) {
            yield return Point<F>.FromPoly(poly, i);
        }
    }
    public static Point<F>[] CreateShares(F secret, int threshold, int total, ISecureRandomNumberGenerator r) {
        return GenerateShares(secret, threshold, r).Take(total).ToArray();
    }
    public static F CombineShares(int degree, IList<Point<F>> shares) {
        var r = TryCombineShares(degree, shares);
        if (r == null) throw new ArgumentException("Inconsistent shares.");
        return r.Item1;
    }
    public static Tuple<F> TryCombineShares(int degree, IList<Point<F>> shares) {
        if (shares.Count < degree) return null;
        var poly = Polynomial<F>.FromInterpolation(shares.Take(degree));
        if (shares.Any(e => !poly.EvaluateAt(e.X).Equals(e.Y))) return null;
        var fieldZero = shares.First().X.Zero;
        return Tuple.Create(poly.EvaluateAt(fieldZero));
    }
}
