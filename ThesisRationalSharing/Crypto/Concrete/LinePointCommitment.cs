using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public static class LinePointCommitment<F> where F : IFiniteField<F>, IEquatable<F> {
    public static Tuple<Polynomial<F>, Point<F>> CreateSignedMessageAndVerifier(F message, ISecureRandomNumberGenerator rng) {
        var b = message.Random(rng);
        var x = message.Random(rng);
        var signedMessageLine = Polynomial<F>.FromCoefficients(new[] { b, message });
        var checkPoint = new Point<F>(x, signedMessageLine.EvaluateAt(x));
        return Tuple.Create(signedMessageLine, checkPoint);
    }
    public static bool AuthenticateMessageUsingVerifier(Polynomial<F> signedMessageLine, Point<F> checkPoint) {
        return signedMessageLine.EvaluateAt(checkPoint.X).Equals(checkPoint.Y);
    }
}
