using BinarySerializer.Nintendo.GBA;

namespace GBAC;

public class CompressionViewModel : BaseViewModel
{
    public CompressionViewModel(string displayName, BaseEncoder encoder, bool includeInSearchDefault, params byte[] validHeaders)
    {
        DisplayName = displayName;
        Encoder = encoder;
        IncludeInSearchDefault = includeInSearchDefault;
        ValidHeaders = validHeaders;
    }

    public string DisplayName { get; }
    public BaseEncoder Encoder { get; }
    public bool IncludeInSearchDefault { get; }
    public byte[] ValidHeaders { get; }
    public bool IncludeInSearch { get; set; }
}