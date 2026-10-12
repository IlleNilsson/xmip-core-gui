namespace Xmip.Operations.Configuration;

/// <summary>
/// A file replaced whole, so a reader never sees half of it: the bytes are
/// written to a temporary file of their own beside it — a name no other
/// writer uses — flushed to the device, then renamed over it. The temporary
/// file is gone afterwards, whether the rename happened or not.
/// </summary>
internal static class WholeFile
{
    /// <summary>Replace <paramref name="path"/> with <paramref name="bytes"/>.</summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be written.</exception>
    public static void Replace(string path, byte[] bytes)
    {
        string temp = $"{path}.{Guid.NewGuid():n}.writing";

        try
        {
            using (FileStream file = new(temp, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
