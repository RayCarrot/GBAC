namespace GBAC;

public class CompressedDataViewModel : BaseViewModel
{
    public CompressedDataViewModel(CompressionViewModel compression, uint offset, uint compressedLength, uint decompressedLength)
    {
        Compression = compression;
        Offset = offset;
        CompressedLength = compressedLength;
        DecompressedLength = decompressedLength;
    }

    public CompressionViewModel Compression { get; }
    public uint Offset { get; }
    public uint CompressedLength { get; }
    public uint DecompressedLength { get; }
}