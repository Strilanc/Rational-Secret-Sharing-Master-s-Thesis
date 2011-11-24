using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/** A bit commitment.
 * Contains the information necessary to recognize a value, but not enough to learn the value. */
public interface ICommitment {
    bool Matches(BigInteger value);
}
public interface ICommitmentScheme {
    ICommitment Create(BigInteger value, ISecureRandomNumberGenerator rng);
}
