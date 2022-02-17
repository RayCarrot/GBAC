using System;
using System.Windows;

namespace GBAC;

public class MessageService
{
    public void DisplayMessage(string message, string header)
    {
        MessageBox.Show(message, header, MessageBoxButton.OK);
    }

    public void DisplayEception(Exception exception, string message)
    {
        MessageBox.Show($"{message}{Environment.NewLine}{Environment.NewLine}Error: {exception.Message}", 
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}