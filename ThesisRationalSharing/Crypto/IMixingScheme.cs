using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/// <summary>Scrambles values using mixes.</summary>
/// <remarks>Examples: sha1(a + b), AES, OneTimePad, a xor b, a + b (mod N)</remarks> */
public interface IMixingScheme<TValue, TMixer> {
    TValue Mix(TValue value, TMixer mix);
}

/// <summary>Reversibly scrambles values using mixes.</summary>
/// <remarks>Examples: OneTimePad, a xor b, a + b (mod N)</remarks> */
public interface IReversibleMixingScheme<TValue, TMixer> : IMixingScheme<TValue, TMixer> {
    TValue Unmix(TValue mixed, TMixer mix);
}
