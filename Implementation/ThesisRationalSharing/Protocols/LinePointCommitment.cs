using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public static class LinePointCommitment {
    public static Tuple<Polynomial<F>, Point<F>> CreateSignedMessageAndVerifier<F>(IFiniteField<F> field, F message, ISecureRandomNumberGenerator rng) {
        var b = field.Random(rng);
        var x = field.Random(rng);
        var signedMessageLine = Polynomial<F>.FromCoefficients(field, new[] { b, message });
        var checkPoint = Point<F>.FromPoly(signedMessageLine, x);
        return Tuple.Create(signedMessageLine, checkPoint);
    }
    public static bool AuthenticateMessageUsingVerifier<F>(Polynomial<F> signedMessageLine, Point<F> checkPoint) {
        return signedMessageLine.EvaluateAt(checkPoint.X).Equals(checkPoint.Y);
    }
}
