using BinarySerializer.GBA;

namespace GBAC;

public class CompressionViewModel : BaseViewModel
{
    public CompressionViewModel(string displayName, GBA_Encoder encoder, bool includeInSearchDefault, params byte[] validHeaders)
    {
        DisplayName = displayName;
        Encoder = encoder;
        IncludeInSearchDefault = includeInSearchDefault;
        ValidHeaders = validHeaders;
    }

    public string DisplayName { get; }
    public GBA_Encoder Encoder { get; }
    public bool IncludeInSearchDefault { get; }
    public byte[] ValidHeaders { get; }
    public bool IncludeInSearch { get; set; }
}