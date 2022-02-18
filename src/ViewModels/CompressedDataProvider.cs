using System.IO;
using BinarySerializer.GBA;

namespace GBAC;

public class CompressedDataProvider
{
    public CompressedDataProvider(byte[] fileData, uint fileOffset)
    {
        Stream = new MemoryStream(fileData);
        FileOffset = fileOffset;
    }

    private MemoryStream Stream { get; }
    private uint FileOffset { get; }

    public byte[] GetData(uint offset, GBA_Encoder encoder)
    {
        lock (Stream)
        {
            Stream.Position = offset - FileOffset;
            using MemoryStream outStream = new();
            encoder.DecodeStream(Stream, outStream);

            return outStream.ToArray();
        }
    }
}