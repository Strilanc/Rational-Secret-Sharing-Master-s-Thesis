using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///<remarks>Example implementation only. Security vulnerabilities are present.</remarks>
[DebuggerDisplay("{ToString()}")]
public class HashCommitment : ICommitment {
    private readonly byte[] _hash;
    private readonly byte[] _salt;

    public HashCommitment(byte[] hash, byte[] salt) {
        Contract.Requires(hash != null);
        Contract.Requires(salt != null);
        this._hash = hash;
        this._salt = salt;
    }

    public static HashCommitment FromValueAndGeneratedSalt(BigInteger value, ISecureRandomNumberGenerator rng) {
        Contract.Requires(rng != null);
        Contract.Ensures(Contract.Result<HashCommitment>() != null);
        var salt = rng.GenerateNextValueMod(BigInteger.One << 128).ToByteArray();
        return FromValueAndSalt(value, salt);
    }
    public static HashCommitment FromValueAndSalt(BigInteger value, byte[] salt) {
        Contract.Requires(salt != null);
        Contract.Ensures(Contract.Result<HashCommitment>() != null);
        return new HashCommitment(SaltedHash(value, salt), salt);
    }

    private static byte[] SaltedHash(BigInteger value, byte[] salt) {
        using (var sha1 = System.Security.Cryptography.SHA1.Create()) {
            return sha1.ComputeHash(salt.Concat(value.ToByteArray()).Concat(salt).ToArray());
        }
    }
    public bool Matches(BigInteger value) {
        using (var sha1 = System.Security.Cryptography.SHA1.Create()) {
            var valueHash = sha1.ComputeHash(_salt.Concat(value.ToByteArray()).Concat(_salt).ToArray());
            return _hash.SequenceEqual(valueHash);
        }
    }

    public override string ToString() {
        return String.Format("Value satisfies sha1(salt ++ Value ++ salt) == {0}, salt = {1}", 
                             "0x" + String.Join("", _hash.Select(b => b.ToString("X"))),
                             "0x" + String.Join("", _salt.Select(b => b.ToString("X"))));
    }
}
