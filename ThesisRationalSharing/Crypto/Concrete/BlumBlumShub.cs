using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics.Contracts;

///<remarks>Example implementation only. Security vulnerabilities are present.</remarks>
public class BlumBlumbShub : ISecureRandomNumberGenerator {
    private ModInt _state;

    /// <param name="modulus">Should be the product of two large primes.</param>
    /// <param name="seed">Seed value in (1, modulus), sharing no common divisors with the modulus.</param>
    public BlumBlumbShub(BigInteger modulus, BigInteger seed) {
        Contract.Requires(1 < seed);
        Contract.Requires(seed < modulus);
        if (BigInteger.GreatestCommonDivisor(modulus, seed) != 1) throw new ArgumentException("Common factor");
        this._state = new ModInt(seed, modulus);
    }

    public bool GenerateNextBit() {
        _state *= _state;
        return _state.Value.IsEven;
    }

    public byte GenerateNextByte(int bits = 8) {
        Contract.Requires(bits >= 0);
        Contract.Requires(bits <= 8);
        byte result = 0;
        for (int i = 0; i < bits; i++) {
            result <<= 1;
            if (GenerateNextBit()) result |= 1;
        }
        return result;
    }
    public BigInteger GenerateNextValueModPowerOf2(int strictPowerCeiling) {
        Contract.Requires(strictPowerCeiling >= 0);
        Contract.Ensures(Contract.Result<BigInteger>() >= 0);
        Contract.Ensures(Contract.Result<BigInteger>() < BigInteger.One << strictPowerCeiling);
        var q = strictPowerCeiling / 8;
        var r = strictPowerCeiling % 8;

        var data = new byte[q + 1]; // need extra byte for remainder bits and sign bit
        for (int i = 0; i < q; i++)
            data[i] = GenerateNextByte();
        data[data.Length - 1] = GenerateNextByte(bits: r);
        return new BigInteger(data);
    }
    public BigInteger GenerateNextValueMod(BigInteger strictCeiling) {
        // ceiling(log(strictCeiling, base: 2))
        int power = 0;
        while (strictCeiling >= BigInteger.One << power)
            power += 1;

        // generate below a power of 2 until it lands within the uniform range
        var c = BigInteger.One << power;
        var m = c - c % strictCeiling;
        while (true) {
            var v = GenerateNextValueModPowerOf2(power);
            if (v < m) return v % strictCeiling;
        }
    }
}
