using System.Text;

namespace ObdLink.Core.Storage;

/// <summary>
/// v0.23.18: writes a file that must survive a power loss right after it was written (the backup taken just before a module
/// write). Written to a temporary file with write-through and flushed to the disk, then renamed into place (never over an
/// existing file), then read back and compared. Only when this returns is the file safe to rely on.
/// </summary>
public static class DurableFile
{
    /// <summary>
    /// Creates <paramref name="path"/> with <paramref name="text"/> (UTF-8, no BOM). Throws <see cref="IOException"/> when the file
    /// already exists or the read-back differs; nothing is left behind at <paramref name="path"/> in that case except an existing file.
    /// </summary>
    public static async Task WriteNewAsync(string path, string text, CancellationToken ct = default)
    {
        var bytes = new UTF8Encoding(false).GetBytes(text);
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        if (File.Exists(path)) throw new IOException($"{Path.GetFileName(path)} already exists; a backup is never overwritten.");
        var tmp = Path.Combine(dir, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await fs.WriteAsync(bytes, ct).ConfigureAwait(false);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: false);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { /* leftover temp file is harmless; it never matches *.asbuilt.json */ }
        }
        var back = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        if (!back.AsSpan().SequenceEqual(bytes))
            throw new IOException($"{Path.GetFileName(path)}: the file read back differs from what was written ({back.Length} of {bytes.Length} bytes). Do not rely on this backup.");
    }

    /// <summary><paramref name="path"/> if it doesn't exist, else the same name with _2, _3 … before <paramref name="suffix"/>.</summary>
    public static string Unique(string path, string suffix)
    {
        if (!File.Exists(path)) return path;
        if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{path} does not end with {suffix}");
        var stem = path[..^suffix.Length];
        for (int n = 2; n < 1000; n++)
        {
            var p = $"{stem}_{n}{suffix}";
            if (!File.Exists(p)) return p;
        }
        throw new IOException($"Too many files named {Path.GetFileName(stem)}*{suffix}");
    }
}
