using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

/** A public key crypto sytem.
 * Anyone with the public key can decrypt messages, but only the holder of the private key can encrypt messages. */
public interface IPublicKeyCryptoScheme<TPublicKey, TPrivateKey, TEncrypted> {
    Tuple<TPublicKey, TPrivateKey> GeneratePublicPrivateKeyPair(ISecureRandomNumberGenerator rng);
    TEncrypted PrivateEncrypt(TPrivateKey privateKey, BigInteger plain);
    BigInteger PublicDecrypt(TPublicKey publicKey, TEncrypted cipher);
}
