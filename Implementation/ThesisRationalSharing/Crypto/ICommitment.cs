using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;


///<summary>A bit commitment. Contains the information necessary to recognize a value, but not enough to learn the value.</summary>
public interface ICommitment<F> {
    bool Matches(F value);
}
///<summary>A bit commitment scheme to create bit commitments for a given type of value.</summary>
public interface ICommitmentScheme<F> {
    ICommitment<F> Create(F value, ISecureRandomNumberGenerator rng);
}
