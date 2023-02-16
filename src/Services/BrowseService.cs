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

    public string? SaveFile(string header, string defaultFileName, string filter = "All Files (*.*)|*.*")
    {
        SaveFileDialog dialog = new()
        {
            Title = header,
            OverwritePrompt = true,
            FileName = defaultFileName,
            Filter = filter
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
            return null;

        return dialog.FileName;
    }
}