namespace GBAC;

public class InfoItemViewModel : BaseViewModel
{
    public InfoItemViewModel(string header, string text)
    {
        Header = header;
        Text = text;
    }

    public string Header { get; }
    public string Text { get; }
}