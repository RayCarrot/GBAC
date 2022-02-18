using BinarySerializer.GBA;

namespace GBAC;

public class CompressionViewModel : BaseViewModel
{
    public CompressionViewModel(string displayName, GBA_Encoder encoder, params byte[] validHeaders)
    {
        DisplayName = displayName;
        Encoder = encoder;
        ValidHeaders = validHeaders;
        IncludeInSearch = true;
    }

    public string DisplayName { get; }
    public GBA_Encoder Encoder { get; }
    public byte[] ValidHeaders { get; }
    public bool IncludeInSearch { get; set; }
}