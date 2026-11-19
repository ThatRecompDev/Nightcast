// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// AES in counter mode with one key, for SRTP's AES-CM (RFC 3711 section 4.1.1) and the counter half of AES-GCM: the
/// last 32 bits of the counter block count up by one each block. Encrypting and decrypting are the same operation.
/// One instance per key, reused for every packet; applying it allocates nothing.
/// </summary>
public sealed class AesCounterMode
{
    private const int CounterOffset = SrtpFormat.BlockLength - sizeof(uint);

    private readonly IBlockCipher _aes = AesUtilities.CreateEngine();
    private readonly byte[] _counter = new byte[SrtpFormat.BlockLength];
    private readonly byte[] _keystream = new byte[SrtpFormat.BlockLength];

    public AesCounterMode(ReadOnlySpan<byte> key)
    {
        _aes.Init(true, new KeyParameter(key));
    }

    /// <summary>Encrypts one block with the bare cipher (GCM's hash key and tag mask).</summary>
    [HotPath]
    public void EncryptBlock(ReadOnlySpan<byte> block, Span<byte> destination)
    {
        _aes.ProcessBlock(block, destination);
    }

    /// <summary>Exclusive-ors <paramref name="data"/> with the keystream that starts at <paramref name="initialCounter"/>.</summary>
    [HotPath]
    public void Apply(ReadOnlySpan<byte> initialCounter, Span<byte> data)
    {
        initialCounter[..SrtpFormat.BlockLength].CopyTo(_counter);
        var counterWord = _counter.AsSpan(CounterOffset);
        uint block = BinaryPrimitives.ReadUInt32BigEndian(counterWord);

        for (int offset = 0; offset < data.Length; offset += SrtpFormat.BlockLength)
        {
            _aes.ProcessBlock(_counter, _keystream);
            int count = Math.Min(SrtpFormat.BlockLength, data.Length - offset);
            SrtpBytes.Xor(data[offset..], _keystream.AsSpan(0, count));
            block++;
            BinaryPrimitives.WriteUInt32BigEndian(counterWord, block);
        }
    }
}
