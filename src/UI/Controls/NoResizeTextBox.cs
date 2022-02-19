using System.Windows;
using System.Windows.Controls;

namespace GBAC;

public class NoResizeTextBox : TextBox
{
    protected override Size MeasureOverride(Size constraint) => new Size(0, 0);
}