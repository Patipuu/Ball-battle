using System;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Opaque player identity used across the connection seam. Wraps the EOS
    /// ProductUserId *string form* so invariant components and their unit tests
    /// never depend on the native <c>Epic.OnlineServices.ProductUserId</c> handle
    /// (which requires the EOS SDK to be loaded, so it cannot be minted in a pure
    /// EditMode test). The EOS lobby / P2P adapters map PlayerId to and from
    /// ProductUserId at the boundary only.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>The EOS ProductUserId in its string form, or null/empty when unset.</summary>
        public string Value { get; }

        public PlayerId(string value) => Value = value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value ?? "<none>";

        public static bool operator ==(PlayerId a, PlayerId b) => a.Equals(b);
        public static bool operator !=(PlayerId a, PlayerId b) => !a.Equals(b);
    }
}
