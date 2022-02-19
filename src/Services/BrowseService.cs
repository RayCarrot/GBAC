using Microsoft.Win32;

namespace GBAC;

public class BrowseService
{
    public string? OpenFile(string header)
    {
        OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Title = header,
            Filter = "All Files (*.*)|*.*"
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
            return null;

        return dialog.FileName;
    }

    public string? SaveFile(string header, string defaultFileName)
    {
        SaveFileDialog dialog = new()
        {
            Title = header,
            OverwritePrompt = true,
            FileName = defaultFileName,
            Filter = "All Files (*.*)|*.*"
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
            return null;

        return dialog.FileName;
    }
}