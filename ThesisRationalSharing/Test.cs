using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

public class Test {
    public static dynamic Bla() {
        return CommitSHA1.FromValue(3).ToString();
        //return new RSA(11, 7).GeneratePublicPrivateKeyPair(new BlumBlumbShub(997 * 991, 25));
    }
    public static Object TryAsyncVerifiedCoalitionShare() {
        var randomNumberGenerator = new BlumBlumbShub(modulus: 997 * 991, seed: 4);
        var publicKeySystem = new RSA(997, 991);
        var shamir = new ShamirSecretSharing(1009);
        var degree = 4;
        var total = 10;
        var coalitionSize = 3;
        var runs = 50;

        var scheme = new AsyncVerifiedProtocol<BigInteger, RSA.Key, RSA.Key>(shamir, publicKeySystem);

        var winRates = new List<dynamic>();
        for (degree = 4; degree < 5; degree++) {
            for (total = 5; total < 10; total++) {
                for (coalitionSize = 2; coalitionSize < 4; coalitionSize++) {
                    var coalitionWinCounts = 0;
                    var otherWinCounts = 0;

                    for (int secret = 0; secret < runs; secret++) {
                        var shares = scheme.Create(secret, degree, total, randomNumberGenerator).Shuffle(randomNumberGenerator);
                        
                        var independents = shares.Skip(coalitionSize).Select(e => scheme.MakeRationalPlayer(e)).ToArray();
                        var coalition = scheme.MakeRationalCoalition(shares.Take(coalitionSize).ToArray());

                        var players = coalition.GetPlayers().Concat(independents).ToArray();
                        var playerRunSecrets = AsyncNetwork<IPlayer, IActorPlayer<ProofValue<BigInteger>>, ProofValue<BigInteger>>.Run(1, players, maxRound: 100);

                        if (playerRunSecrets.Keys.Intersect(coalition.GetPlayers()).Count() < coalitionSize) {
                            coalitionWinCounts += 0;
                        }
                        foreach (var x in playerRunSecrets) {
                            if (x.Key is AsyncVerifiedProtocol<BigInteger, RSA.Key, RSA.Key>.RationalPlayer) {
                                otherWinCounts += 1;
                            } else {
                                coalitionWinCounts += 1;
                            }
                        }
                    }
                    var coalWinRate = coalitionWinCounts / ((double)runs * coalitionSize);
                    var indWinRate = otherWinCounts / ((double)runs * (total - coalitionSize));
                    winRates.Add(new { N = total, M = degree, C = coalitionSize, cr = coalWinRate, ir = indWinRate });
                }
            }
        }

        return String.Join(Environment.NewLine, winRates.Select(e => String.Format("{0}\t{1}\t{2}\t{3:0.00}\t{4:0.00}", e.N, e.M, e.C, e.cr, e.ir)));
    }
    public static Object TryLargeInterpolate() {
        var b = BigInteger.Zero;
        var m = 1009;

        var f1 = ModIntPolynomial.FromInterpolation(new[] { 1, 2 }.Select((e, i) => Tuple.Create(b + i, b + e)).ToArray(), m);
        var f2 = ModIntPolynomial.FromInterpolation(new[] { 1, 2 }.Select((e, i) => Tuple.Create(b + i, b + e)).ToArray(), m);
        var ff = f1 * f2;
        var qr1 = ff.DivRem(f1);
        var qr2 = ff.DivRem(f2);
        var g1 = qr1.Item1 * f1 + qr1.Item2;
        var g2 = qr2.Item1 * f2 + qr2.Item2;

        var v = new[] { 5, 5, 5, 5, 5, 5, 5, 5, 6 };
        var p = ModIntPolynomial.FromInterpolation(v.Select((e, i) => Tuple.Create(b + i, b + e)).ToArray(), m);
        var q = ModIntPolynomial.FromInterpolation(Enumerable.Range(50, 900).Select(e => Tuple.Create(b + e, p.EvaluateAt(e).Value)).ToArray(), m);
        return p == q;
    }
    public static bool IsPrime(int n) {
        var s = (int)Math.Ceiling(Math.Sqrt(n));
        for (int i = 2; i <= s; i++) {
            if (n % i == 0) return false;
        }
        return true;
    }
}
