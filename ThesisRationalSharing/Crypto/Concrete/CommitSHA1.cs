using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///<remarks>Example implementation only. Security vulnerabilities are present.</remarks>
[DebuggerDisplay("{ToString()}")]
public class CommitSHA1 : ICommitment {
    private readonly byte[] _hash;

    public CommitSHA1(byte[] hash) {
        Contract.Requires(hash != null);
        this._hash = hash;
    }

    public static CommitSHA1 FromValue(BigInteger value) {
        Contract.Ensures(Contract.Result<CommitSHA1>() != null);
        return new CommitSHA1(Hash(value));
    }

    private static byte[] Hash(BigInteger value) {
        using (var sha1 = System.Security.Cryptography.SHA1.Create()) {
            return sha1.ComputeHash(value.ToByteArray());
        }
    }
    public bool Matches(BigInteger value) {
        var valueHash = Hash(value);
        return _hash.SequenceEqual(valueHash);
    }

    public override string ToString() {
        return String.Format("sha1(?) == 0x" + String.Join("", _hash.Select(b => b.ToString("X"))));
    }
}

public class CommitSHA1Scheme : ICommitmentScheme {
    public ICommitment Create(BigInteger value, ISecureRandomNumberGenerator rng) {
        return CommitSHA1.FromValue(value);
    }
}
