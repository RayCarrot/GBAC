namespace GBAC;

public class CompressedDataViewModel : BaseViewModel
{
    public CompressedDataViewModel(CompressionViewModel compression, uint offset, long compressedLength, long decompressedLength)
    {
        Compression = compression;
        Offset = offset;
        CompressedLength = compressedLength;
        DecompressedLength = decompressedLength;
    }

    public CompressionViewModel Compression { get; }
    public uint Offset { get; }
    public long CompressedLength { get; }
    public long DecompressedLength { get; }
}