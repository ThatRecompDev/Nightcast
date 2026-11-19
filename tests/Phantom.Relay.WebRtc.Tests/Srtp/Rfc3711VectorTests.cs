// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Srtp;

/// <summary>The test vectors of RFC 3711 appendix B: the AES-CM keystream (B.2) and the key derivation (B.3), as data.</summary>
public sealed class Rfc3711VectorTests
{
    private static readonly byte[] KeystreamKey = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
    private static readonly byte[] KeystreamOffset = Convert.FromHexString("F0F1F2F3F4F5F6F7F8F9FAFBFCFD0000");

    private static readonly byte[] MasterKey = Convert.FromHexString("E1F97A0D3E018BE0D64FA32C06DE4139");
    private static readonly byte[] MasterSalt = Convert.FromHexString("0EC675AD498AFEEBB6960B3AABE6");

    [Fact]
    public void The_keystream_starts_with_the_first_three_blocks_of_b2()
    {
        var keystream = new byte[48];

        new AesCounterMode(KeystreamKey).Apply(KeystreamOffset, keystream);

        Assert.Equal(
            "E03EAD0935C95E80E166B16DD92B4EB4" + "D23513162B02D0F72A43A2FE4A5F97AB" + "41E95B3BB0A2E8DD477901E4FCA894C0",
            Convert.ToHexString(keystream));
    }

    [Fact]
    public void The_keystream_ends_with_the_last_three_blocks_of_b2()
    {
        var keystream = new byte[1044512];

        new AesCounterMode(KeystreamKey).Apply(KeystreamOffset, keystream);

        Assert.Equal(
            "EC8CDF7398607CB0F2D21675EA9EA1E4" + "362B7C3C6773516318A077D7FC5073AE" + "6A2CC3787889374FBEB4C81B17BA6C44",
            Convert.ToHexString(keystream.AsSpan(keystream.Length - 48)));
    }

    [Fact]
    public void Applying_the_keystream_twice_gives_the_plaintext_back()
    {
        var cipher = new AesCounterMode(KeystreamKey);
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];
        var copy = (byte[])data.Clone();

        cipher.Apply(KeystreamOffset, copy);
        cipher.Apply(KeystreamOffset, copy);

        Assert.Equal(data, copy);
    }

    [Fact]
    public void The_cipher_key_is_the_one_of_b3()
    {
        var key = SrtpKeyDerivation.Derive(MasterKey, MasterSalt, SrtpFormat.RtpEncryptionLabel, 16);

        Assert.Equal("C61E7A93744F39EE10734AFE3FF7A087", Convert.ToHexString(key));
    }

    [Fact]
    public void The_cipher_salt_is_the_one_of_b3()
    {
        var salt = SrtpKeyDerivation.Derive(MasterKey, MasterSalt, SrtpFormat.RtpSaltLabel, 14);

        Assert.Equal("30CBBC08863D8C85D49DB34A9AE1", Convert.ToHexString(salt));
    }

    [Fact]
    public void The_auth_key_is_the_94_octets_of_b3()
    {
        var authKey = SrtpKeyDerivation.Derive(MasterKey, MasterSalt, SrtpFormat.RtpAuthenticationLabel, 94);

        Assert.Equal(
            "CEBE321F6FF7716B6FD4AB49AF256A15" + "6D38BAA48F0A0ACF3C34E2359E6CDBCE" + "E049646C43D9327AD175578EF7227098"
            + "6371C10C9A369AC2F94A8C5FBCDDDC25" + "6D6E919A48B610EF17C2041E47403576" + "6B68642C59BBFC2F34DB60DBDFB2",
            Convert.ToHexString(authKey));
    }

    [Fact]
    public void A_master_key_of_another_length_is_refused()
    {
        Assert.Throws<ArgumentException>(() => SrtpKeyDerivation.Derive(new byte[15], MasterSalt, SrtpFormat.RtpEncryptionLabel, 16));
    }
}
