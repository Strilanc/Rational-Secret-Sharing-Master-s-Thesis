using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.Diagnostics;

public static class LinePointCommitment {
    public static Tuple<ModIntPolynomial, ModPoint> CreateSignedMessageAndVerifier(ModInt message, ISecureRandomNumberGenerator rng) {
        var N = message.Modulus;
        var m = message.Value;
        var b = rng.GenerateNextValueMod(N);
        var x = rng.GenerateNextValueMod(N);
        var signedMessageLine = new ModIntPolynomial(new[] { b, m }, N);
        var checkPoint = new ModPoint(x, signedMessageLine.EvaluateAt(x).Value, N);
        return Tuple.Create(signedMessageLine, checkPoint);
    }
    public static bool AuthenticateMessageUsingVerifier(ModIntPolynomial signedMessageLine, ModPoint checkPoint) {
        return signedMessageLine.EvaluateAt(checkPoint.X) == checkPoint.Y;
    }
}
