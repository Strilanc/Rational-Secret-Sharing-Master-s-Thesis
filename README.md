# Rational Secret Sharing

My master's thesis on rational secret sharing.
Includes the thesis itself (source latex + output pdf), an implementation of the protocols described (C#), and the slides from my defense.

*Secret sharing* is a method of splitting a secret value into parts in a way that requires k out of the n parts to put the secret back together.
This problem was solved in the 70s by [Shamir's Secret Sharing Scheme](https://en.wikipedia.org/wiki/Shamir%27s_Secret_Sharing), which is based on interpolating (k-1)-degree polynomials over finite fields from k sample points.

Rational secret sharing extends the problem by asking "What if the players putting the secret back together are greedy?".
That is to say, suppose every player really wants to learn the secret, but as a secondary goal they want to prevent the other players from learning the secret.
Also, the greedy players may form into coalitions that work together to prevent the others from learning the secret.

The thesis in this repository presents four protocols, each for a different variation of the problem:

1. (SUIP) Opponents are computationally unbounded (i.e. can break any cryptography not based on information-theoretic security), and a synchronous broadcast communication mechanism is available.
2. (SBP) A synchronous broadcast communication mechanism is available.
3. (ABIP) Players can't tell whether a given value is the secret or not during the protocol (e.g. it's the combination to a burn safe).
4. (ABCP) The 'normal' case. Asynchronous communication, opponents can't break cryptography, secret is recognizable.

The thesis also argues that these protocols are correct, with some caveats related to how effective any protocol can be.
