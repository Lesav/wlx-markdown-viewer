using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MarkdownView.Wpf;

internal static class ScrollPositionStore
{
    private const string RegistryPath = @"Software\MarkdownView\ScrollPositions";
    private const string FilePathValue = "FilePath";
    private const string VerticalOffsetValue = "VerticalOffset";
    private const string LastClosedUtcTicksValue = "LastClosedUtcTicks";
    private const int MaximumEntries = 50;
    private static readonly object SyncRoot = new();

    internal static double? Load(string fileName)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(fileName);
            var entryName = EntryName(normalizedPath);
            lock (SyncRoot)
            {
                using var root = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                using var entry = root?.OpenSubKey(entryName, false);
                if (entry is null ||
                    entry.GetValue(FilePathValue) is not string storedPath ||
                    !string.Equals(storedPath, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                    entry.GetValue(VerticalOffsetValue) is not string storedOffset ||
                    !double.TryParse(storedOffset, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var offset) ||
                    double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0)
                    return null;

                return offset;
            }
        }
        catch (Exception)
        {
            // Registry persistence is optional and must never prevent the viewer from opening.
            return null;
        }
    }

    internal static void Save(string fileName, double verticalOffset)
    {
        if (double.IsNaN(verticalOffset) || double.IsInfinity(verticalOffset))
            return;

        try
        {
            var normalizedPath = Path.GetFullPath(fileName);
            verticalOffset = Math.Max(0, verticalOffset);
            lock (SyncRoot)
            {
                using var root = Registry.CurrentUser.CreateSubKey(RegistryPath, true);
                if (root is null)
                    return;

                using (var entry = root.CreateSubKey(EntryName(normalizedPath), true))
                {
                    if (entry is null)
                        return;
                    entry.SetValue(FilePathValue, normalizedPath, RegistryValueKind.String);
                    entry.SetValue(VerticalOffsetValue,
                        verticalOffset.ToString("R", CultureInfo.InvariantCulture),
                        RegistryValueKind.String);
                    entry.SetValue(LastClosedUtcTicksValue, DateTime.UtcNow.Ticks,
                        RegistryValueKind.QWord);
                }

                PruneOldestEntries(root);
            }
        }
        catch (Exception)
        {
            // Closing the viewer must succeed even when the registry is unavailable or read-only.
        }
    }

    private static string EntryName(string normalizedPath)
    {
        using var algorithm = SHA256.Create();
        var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant()));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static void PruneOldestEntries(RegistryKey root)
    {
        var entries = new List<StoredEntry>();
        foreach (var entryName in root.GetSubKeyNames())
        {
            long closedTicks = 0;
            try
            {
                using var entry = root.OpenSubKey(entryName, false);
                if (entry?.GetValue(LastClosedUtcTicksValue) is long storedTicks)
                    closedTicks = storedTicks;
            }
            catch (Exception)
            {
                // Malformed or inaccessible entries are treated as the oldest ones.
            }
            entries.Add(new StoredEntry(entryName, closedTicks));
        }

        foreach (var entry in entries
            .OrderByDescending(item => item.LastClosedUtcTicks)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Skip(MaximumEntries))
        {
            try
            {
                root.DeleteSubKeyTree(entry.Name, false);
            }
            catch (Exception)
            {
                // A concurrent viewer instance may already have changed this entry.
            }
        }
    }

    private sealed class StoredEntry
    {
        internal StoredEntry(string name, long lastClosedUtcTicks)
        {
            Name = name;
            LastClosedUtcTicks = lastClosedUtcTicks;
        }

        internal string Name { get; }
        internal long LastClosedUtcTicks { get; }
    }
}
