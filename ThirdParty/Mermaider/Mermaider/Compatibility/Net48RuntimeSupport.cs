#if NET48
using System;
using System.Collections.Generic;

namespace System
{
    internal readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _value = fromEnd ? ~value : value;
        }

        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;
        public static Index Start => new Index(0);
        public static Index End => new Index(0, true);
        public static implicit operator Index(int value) => new Index(value);
        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;
        public bool Equals(Index other) => _value == other._value;
        public override bool Equals(object obj) => obj is Index other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsFromEnd ? "^" + Value : Value.ToString();
    }

    internal readonly struct Range : IEquatable<Range>
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }
        public Index End { get; }
        public static Range All => new Range(Index.Start, Index.End);
        public static Range StartAt(Index start) => new Range(start, Index.End);
        public static Range EndAt(Index end) => new Range(Index.Start, end);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            var start = Start.GetOffset(length);
            var end = End.GetOffset(length);
            if ((uint)end > (uint)length || (uint)start > (uint)end)
                throw new ArgumentOutOfRangeException(nameof(length));
            return (start, end - start);
        }

        public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);
        public override bool Equals(object obj) => obj is Range other && Equals(other);
        public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();
        public override string ToString() => Start + ".." + End;
    }

    internal static class Net48StringExtensions
    {
        public static bool Contains(this string value, string part, StringComparison comparison) =>
            value.IndexOf(part, comparison) >= 0;

        public static bool Contains(this string value, char character) => value.IndexOf(character) >= 0;
        public static bool Contains(this string value, char character, StringComparison comparison) => value.IndexOf(character) >= 0;
        public static bool StartsWith(this string value, char character) => value.Length > 0 && value[0] == character;
        public static bool EndsWith(this string value, char character) => value.Length > 0 && value[value.Length - 1] == character;

        public static string Replace(this string value, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(oldValue))
                throw new ArgumentException("Value cannot be empty.", nameof(oldValue));

            var start = 0;
            var match = value.IndexOf(oldValue, start, comparison);
            if (match < 0)
                return value;

            var result = new Text.StringBuilder(value.Length);
            while (match >= 0)
            {
                result.Append(value, start, match - start);
                result.Append(newValue);
                start = match + oldValue.Length;
                match = value.IndexOf(oldValue, start, comparison);
            }
            result.Append(value, start, value.Length - start);
            return result.ToString();
        }

        public static string[] Split(this string value, char separator, StringSplitOptions options) =>
            value.Split(new[] { separator }, options);

        public static bool StartsWith(this ReadOnlySpan<char> value, string prefix) =>
            value.Length >= prefix.Length && value.Slice(0, prefix.Length).SequenceEqual(prefix.AsSpan());

        public static bool StartsWith(this ReadOnlySpan<char> value, string prefix, StringComparison comparison) =>
            value.ToString().StartsWith(prefix, comparison);

        public static bool EndsWith(this ReadOnlySpan<char> value, string suffix) =>
            value.Length >= suffix.Length && value.Slice(value.Length - suffix.Length).SequenceEqual(suffix.AsSpan());

        public static bool EndsWith(this ReadOnlySpan<char> value, string suffix, StringComparison comparison) =>
            value.ToString().EndsWith(suffix, comparison);

        public static int IndexOf(this ReadOnlySpan<char> value, string text) => value.IndexOf(text.AsSpan());
        public static bool Contains(this ReadOnlySpan<char> value, char character) => value.IndexOf(character) >= 0;
    }
}

namespace System.Collections.Generic
{
    internal static class Net48CollectionExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
            dictionary.TryGetValue(key, out var value) ? value : default;

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue) =>
            dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, value);
            return true;
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}

namespace Mermaider.Compatibility
{
    internal static class Net48Compat
    {
        internal static T Clamp<T>(T value, T min, T max) where T : IComparable<T> =>
            value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;

        internal static T ParseEnum<T>(string value, bool ignoreCase = false) where T : struct =>
            (T)Enum.Parse(typeof(T), value, ignoreCase);

        internal static string[] SplitAndTrim(string value, char[] separators, bool removeEmpty)
        {
            var options = removeEmpty ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
            return value.Split(separators, options).Select(part => part.Trim()).ToArray();
        }

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static bool IsAsciiLetterOrDigit(char value) =>
            (value >= 'A' && value <= 'Z') ||
            (value >= 'a' && value <= 'z') ||
            (value >= '0' && value <= '9');
    }
}
#endif
