using System;
using System.IO;
using BinarySerializer.GBA;

namespace GBAC;

public class FileDataProvider
{
    public FileDataProvider(byte[] fileData, uint fileOffset, string filePath)
    {
        FileData = fileData;
        Stream = new MemoryStream(fileData);
        FileOffset = fileOffset;
        FilePath = filePath;
    }

    private byte[] FileData { get; }
    private MemoryStream Stream { get; }
    private uint FileOffset { get; }
    private string FilePath { get; }

    public byte[] GetData(uint offset, int length, bool allowPartialRead = true)
    {
        if (!IsOffsetValid(offset))
            throw new ArgumentException($"Invalid offset {offset}", nameof(offset));

        // TODO: If this offset matches a compressed data offset then read from the compressed data?

        lock (Stream)
        {
            Stream.Position = offset - FileOffset;

            byte[] buffer = new byte[length];
            int read = Stream.Read(buffer, 0, length);

            if (!allowPartialRead && read != length)
                throw new EndOfStreamException("Could not read all requested bytes");
            
            return buffer;
        }
    }

    public byte[] GetData(uint offset, GBA_Encoder encoder)
    {
        if (!IsOffsetValid(offset))
            throw new ArgumentException($"Invalid offset {offset}", nameof(offset));

        lock (Stream)
        {
            Stream.Position = offset - FileOffset;
            using MemoryStream outStream = new();
            encoder.DecodeStream(Stream, outStream);

            return outStream.ToArray();
        }
    }

    public void OverwriteData(uint offset, byte[] data)
    {
        Array.Copy(data, 0, FileData, offset - FileOffset, data.Length);
        File.WriteAllBytes(FilePath, FileData);
    }

    public bool IsOffsetValid(uint offset)
    {
        return offset >= FileOffset && offset < FileOffset + Stream.Length;
    }
}