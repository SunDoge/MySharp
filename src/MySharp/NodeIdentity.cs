using System.Buffers.Binary;
using System.Numerics;

namespace MySharp;

// Numeric parent/scope composition follows MyGo's design. Full address metadata is
// retained to diagnose a hash collision instead of silently aliasing controls.
internal readonly record struct IdentityKey(byte Kind, ulong Hash, string? Text = null,
    int Line = 0, int Occurrence = 0, long Number = 0, Guid Guid = default)
{
    internal static IdentityKey Caller(string file, int line, int occurrence, ulong source) =>
        new(1, IdentityMath.Mix(source, (ulong)occurrence), file, line, occurrence);
    internal static IdentityKey Explicit(string key) => new(2, IdentityMath.Mix(IdentityMath.Text(key), 2), key);
    internal static IdentityKey Site(ulong site, string? key = null) =>
        new(3, IdentityMath.Mix(site, key is null ? 0 : IdentityMath.Text(key)), key, Number: unchecked((long)site));
    internal static IdentityKey StringScope(string key) => new(4, IdentityMath.Mix(IdentityMath.Text(key), 4), key);
    internal static IdentityKey LongScope(long key) => new(5, IdentityMath.Mix(unchecked((ulong)key), 5), Number: key);
    internal static IdentityKey GuidScope(Guid key)
    {
        Span<byte> bytes = stackalloc byte[16];
        key.TryWriteBytes(bytes);
        return new(6, IdentityMath.Mix(BinaryPrimitives.ReadUInt64LittleEndian(bytes), BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..])), Guid: key);
    }
    public override int GetHashCode() => Hash.GetHashCode();
}

internal readonly record struct NodeAddress(ulong Parent, ulong Scope, IdentityKey Key)
{
    internal ulong Id(bool scope = false)
    {
        var hash = IdentityMath.Mix(IdentityMath.Mix(IdentityMath.Mix(scope ? 2UL : 1UL, Parent), Scope), IdentityMath.Mix(Key.Hash, Key.Kind));
        return hash == 0 ? ulong.MaxValue : hash; // zero is the root/no-scope sentinel
    }
}

internal static class IdentityMath
{
    internal static ulong Text(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash = unchecked((hash ^ (byte)c) * 1099511628211UL);
            hash = unchecked((hash ^ (byte)(c >> 8)) * 1099511628211UL);
        }
        return hash;
    }
    // SplitMix64 finalization; unlike hash-code strings, this allocates nothing.
    internal static ulong Mix(ulong left, ulong right)
    {
        var value = unchecked(left + BitOperations.RotateLeft(right, 27) + 0x9E3779B97F4A7C15UL);
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }
}
