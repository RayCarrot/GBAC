using Microsoft.Win32;

namespace GBAC;

public class BrowseService
{
    public string? BrowseFile(string header)
    {
        OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Title = header,
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
            return null;

        return dialog.FileName;
    }
}