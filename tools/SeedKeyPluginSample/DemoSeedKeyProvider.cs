using ObdLink.Core.Uds;

namespace ObdLink.SeedKey.Sample;

/// <summary>
/// Example plug-in. It only answers the DEMO simulator (fixed seed 11 22 33, key = seed reversed) and returns null for
/// everything else, so it can never send a guessed key to a real module (a wrong key costs an attempt and can start a lockout delay).
///
/// To write your own: implement <see cref="ISeedKeyProvider"/> in a public class with a parameterless constructor.
/// Return null for any module or level you do not handle; the app then tries the next plug-in.
/// The app ships no OEM algorithms or keys. You are responsible for where yours come from.
/// </summary>
public sealed class DemoSeedKeyProvider : ISeedKeyProvider
{
    private static readonly byte[] DemoSeed = { 0x11, 0x22, 0x33 };

    public Task<byte[]?> ComputeKeyAsync(uint moduleTxId, byte level, byte[] seed, CancellationToken ct = default)
        => Task.FromResult(seed.AsSpan().SequenceEqual(DemoSeed) ? seed.Reverse().ToArray() : null);
}
