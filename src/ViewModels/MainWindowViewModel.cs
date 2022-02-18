using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using BinarySerializer.GBA;

namespace GBAC;

public class MainWindowViewModel : BaseViewModel
{
    #region Constructor

    public MainWindowViewModel()
    {
        Message = new MessageService();
        Browse = new BrowseService();

        FileOffset = GBAConstants.Address_ROM;

        CompressionTypes = new CompressionViewModel[]
        {
            new CompressionViewModel("LZSS", new GBA_LZSSEncoder(), 0x10),
            new CompressionViewModel("Huffman", new GBA_HuffmanEncoder(), 0x24, 0x28),
            new CompressionViewModel("RLE", new GBA_RLEEncoder(), 0x30),
        };
        CompressedData = new ObservableCollection<CompressedDataViewModel>();

        BindingOperations.EnableCollectionSynchronization(CompressedData, CompressedData);

        BrowseFileCommand = new RelayCommand(BrowseFile);
        LoadFileCommand = new RelayCommand(LoadFile);
        UnloadFileCommand = new RelayCommand(UnloadFile);
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        StopSearchCommand = new RelayCommand(StopSearch);
        ClearDataCommand = new RelayCommand(ClearData);

        SetTitle();
    }

    #endregion

    #region Services

    public MessageService Message { get; }
    public BrowseService Browse { get; }

    #endregion

    #region Commands

    public ICommand BrowseFileCommand { get; }
    public ICommand LoadFileCommand { get; }
    public ICommand UnloadFileCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand StopSearchCommand { get; }
    public ICommand ClearDataCommand { get; }

    #endregion

    #region Private Fields

    private readonly HashSet<uint> _foundDataOffsets = new(); // Local file offsets

    #endregion

    #region Public Properties

    // Window
    public string Title { get; set; }

    // File
    public string? FilePath { get; set; }
    public uint FileOffset { get; set; }

    // Data
    public byte[]? FileData { get; set; }
    public bool IsLoaded => FileData != null;

    // Search
    public bool IsSearching { get; set; }
    public double SearchProgress { get; set; }
    public double SearchProgressMax { get; set; }
    public bool CancelSearch { get; set; }

    // Search Options
    public uint MinSearchOffset { get; set; }
    public uint MaxSearchOffset { get; set; }
    public uint MinDecompressedSize { get; set; }
    public uint MaxDecompressedSize { get; set; }

    // Compression
    public CompressionViewModel[] CompressionTypes { get; }
    public ObservableCollection<CompressedDataViewModel> CompressedData { get; }

    #endregion

    #region Public Methods

    public void BrowseFile()
    {
        string? filePath = Browse.BrowseFile("Select the game file");

        if (filePath == null)
            return;

        FilePath = filePath;
    }

    public void LoadFile()
    {
        UnloadFile();

        try
        {
            FileData = File.ReadAllBytes(FilePath ?? String.Empty);
            SetTitle(Path.GetFileName(FilePath));

            MinSearchOffset = FileOffset;
            MaxSearchOffset = (uint)(FileOffset + FileData.Length);

            MinDecompressedSize = 0x10;
            MaxDecompressedSize = 0xFFFF;

            foreach (CompressionViewModel c in CompressionTypes)
                c.IncludeInSearch = true;
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when loading the file");
        }
    }

    public void UnloadFile()
    {
        if (IsSearching)
            throw new Exception("Can't unload file while searching for data");

        FileData = null;
        ClearData();
        SetTitle();
    }

    public async Task SearchAsync()
    {
        if (FileData == null || IsSearching)
            return;

        if (MinSearchOffset < FileOffset || MaxSearchOffset > FileOffset + FileData.Length)
        {
            Message.DisplayMessage("The range is invalid! Must be within the file.", "Invalid range");
            return;
        }

        IsSearching = true;
        CancelSearch = false;

        try
        {
            SearchProgress = 0;

            // TODO: Allow this to be specified
            const int align = 1;

            uint max = MaxSearchOffset - FileOffset - 4;
            SearchProgressMax = max;

            await Task.Run(() =>
            {
                using MemoryStream stream = new(FileData);

                CompressionViewModel[] compressionTypes = CompressionTypes.Where(x => x.IncludeInSearch).ToArray();

                for (uint i = MinSearchOffset - FileOffset; i < max; i += 4)
                {
                    SearchProgress = i;

                    if (CancelSearch)
                        break;

                    if (_foundDataOffsets.Contains(i))
                        continue;

                    uint size = ((uint)FileData[i + 3] << 16) | ((uint)FileData[i + 2] << 8) | FileData[i + 1];

                    if (size > MaxDecompressedSize || size < MinDecompressedSize)
                        continue;

                    if (align > 1 && size % align != 0)
                        continue;

                    foreach (CompressionViewModel c in compressionTypes)
                    {
                        if (!c.ValidHeaders.Contains(FileData[i]))
                            continue;

                        stream.Position = i;

                        // TODO: Rather than using a memory stream we can create a dummy stream which only tracks the position
                        using MemoryStream outStream = new();

                        try
                        {
                            c.Encoder.DecodeStream(stream, outStream);
                        }
                        catch
                        {
                            continue;
                        }

                        if (outStream.Length != size)
                            continue;

                        CompressedData.Add(new CompressedDataViewModel(c, FileOffset + i, stream.Position - i, outStream.Length));
                        _foundDataOffsets.Add(i);
                        break;
                    }
                }
            });
        }
        finally
        {
            IsSearching = false;
        }
    }

    public void StopSearch()
    {
        CancelSearch = true;
    }

    public void ClearData()
    {
        if (IsSearching)
            throw new Exception("Can't clear data while searching for data");

        CompressedData.Clear();
        _foundDataOffsets.Clear();
    }

    [MemberNotNull(nameof(Title))]
    public void SetTitle(string? subTitle = null)
    {
        Title = "GBAC 1.0.0.0";

        if (subTitle != null)
            Title += $" - {subTitle}";
    }

    #endregion
}