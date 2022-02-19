using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace GBAC;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // TODO: Inject using DI
        DataContext = ViewModel = new MainWindowViewModel(new MessageService(), new BrowseService());
    }

    public MainWindowViewModel ViewModel { get; }

    private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (CompressedDataViewModel c in e.RemovedItems.Cast<CompressedDataViewModel>())
            c.Unload();

        foreach (CompressedDataViewModel c in e.AddedItems.Cast<CompressedDataViewModel>())
            c.Load();
    }

    private void TilePreviewWidthNumericUpDown_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
    {
        ((sender as FrameworkElement)?.DataContext as CompressedDataViewModel)?.LoadTilePreviews();
    }

    private void MapPreviewWidthNumericUpDown_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
    {
        ((sender as FrameworkElement)?.DataContext as CompressedDataViewModel)?.LoadMapPreviews();
    }
}