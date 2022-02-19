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

    public MainWindowViewModel(MessageService messageService, BrowseService browseService)
    {
        Message = messageService;
        Browse = browseService;

        FileOffset = GBAConstants.Address_ROM;

        CompressionTypes = new CompressionViewModel[]
        {
            new CompressionViewModel("LZSS", new GBA_LZSSEncoder(), true, 0x10),
            new CompressionViewModel("Huffman", new GBA_HuffmanEncoder(), false, 0x24, 0x28),
            new CompressionViewModel("RLE", new GBA_RLEEncoder(), false, 0x30),
        };
        CompressedData = new ObservableCollection<CompressedDataViewModel>();

        BindingOperations.EnableCollectionSynchronization(CompressedData, CompressedData);

        BrowseFileCommand = new RelayCommand(BrowseFile);
        LoadFileCommand = new RelayCommand(LoadFile);
        UnloadFileCommand = new RelayCommand(UnloadFile);
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        StopSearchCommand = new RelayCommand(StopSearch);
        ClearDataCommand = new RelayCommand(ClearData);
        FindBytesCommand = new RelayCommand(FindBytes);

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
    public ICommand FindBytesCommand { get; }

    #endregion

    #region Private Fields

    private readonly HashSet<uint> _foundDataOffsets = new(); // Local file offsets
    private FileDataProvider? _dataCache;

    #endregion

    #region Public Properties

    // Window
    public string Title { get; set; }

    // File
    public string? FilePath { get; set; }
    public uint FileOffset { get; set; }

    // Data
    public byte[]? FileData { get; set; }
    public Dictionary<uint, uint[]>? FileReferences { get; set; }
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
    public uint DecompressedDataAlign { get; set; }

    // Compression
    public CompressionViewModel[] CompressionTypes { get; }
    public ObservableCollection<CompressedDataViewModel> CompressedData { get; }
    public CompressedDataViewModel? SelectedData { get; set; }

    // Find
    public string? FindBytesInput { get; set; }

    #endregion

    #region Private Methods

    private static Dictionary<uint, uint[]> FindFileReferences(byte[] fileData, uint fileOffset)
    {
        Dictionary<uint, HashSet<uint>> refs = new();

        uint fileEnd = (uint)(fileOffset + fileData.Length);

        for (uint i = 4 - (fileOffset % 4); i < fileData.Length; i += 4)
        {
            uint pointer = ((uint)fileData[i + 3] << 24) | ((uint)fileData[i + 2] << 16) | ((uint)fileData[i + 1] << 8) | fileData[i + 0];

            if (pointer < fileOffset || pointer >= fileEnd) 
                continue;
            
            if (!refs.ContainsKey(pointer))
                refs[pointer] = new HashSet<uint>();

            refs[pointer].Add(i + fileOffset);
        }

        return refs.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    private static byte[] StringToByteArray(string hex)
    {
        hex = hex.Replace(" ", "");

        byte[] arr = new byte[hex.Length >> 1];

        for (int i = 0; i < hex.Length >> 1; ++i)
            arr[i] = (byte)((GetHexVal((byte)hex[i << 1]) << 4) + (GetHexVal((byte)hex[(i << 1) + 1])));

        return arr;
    }

    private static int GetHexVal(byte hex)
    {
        return hex - (hex < 58 ? 48 : (hex < 97 ? 55 : 87));
    }

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
            _dataCache = new FileDataProvider(FileData, FileOffset);

            FileReferences = FindFileReferences(FileData, FileOffset);

            SetTitle(Path.GetFileName(FilePath));

            MinSearchOffset = FileOffset;
            MaxSearchOffset = (uint)(FileOffset + FileData.Length);

            MinDecompressedSize = 0x10;
            MaxDecompressedSize = 0xFFFF;
            DecompressedDataAlign = 1;

            foreach (CompressionViewModel c in CompressionTypes)
                c.IncludeInSearch = c.IncludeInSearchDefault;
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
        FileReferences = null;
        _dataCache = null;
        FindBytesInput = String.Empty;
        ClearData();
        SetTitle();
    }

    public async Task SearchAsync()
    {
        if (IsSearching)
            return;

        if (FileData == null || _dataCache == null || FileReferences == null)
        {
            Message.DisplayMessage("A search can not be performed due to the file not being correctly loaded", "Error");
            return;
        }

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

            uint max = MaxSearchOffset - FileOffset;

            // Fix for searching a single address using same min and max
            if (MinSearchOffset == MaxSearchOffset)
                max++;

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

                    if (DecompressedDataAlign > 1 && size % DecompressedDataAlign != 0)
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

                        uint fileOffset = FileOffset + i;

                        CompressedData.Add(new CompressedDataViewModel(
                            messageService: Message, 
                            dataProvider: _dataCache, 
                            compression: c, 
                            offset: fileOffset, 
                            compressedLength: (uint)(stream.Position - i), 
                            decompressedLength: (uint)outStream.Length, 
                            references: FileReferences.TryGetValue(fileOffset, out uint[] refs) ? refs : Array.Empty<uint>()));

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
        SelectedData?.Unload();
        SelectedData = null;
        _foundDataOffsets.Clear();
    }

    public void FindBytes()
    {
        if (FindBytesInput == null)
            return;

        byte[] bytes = StringToByteArray(FindBytesInput);

        int matches = 0;

        foreach (CompressedDataViewModel c in CompressedData)
        {
            c.IsHighlighted = false;

            if (bytes.Length == 0)
                continue;

            byte[] data = c.GetData();

            for (int i = 0; i < data.Length - bytes.Length + 1; i++)
            {
                if (data[i] != bytes[0]) 
                    continue;

                bool found = true;
                
                for (int j = 1; j < bytes.Length; j++)
                {
                    if (data[i + j] == bytes[j]) 
                        continue;
                    
                    found = false;
                    break;
                }

                if (!found) 
                    continue;

                matches++;
                c.IsHighlighted = true;
                break;
            }
        }

        Message.DisplayMessage($"Found {matches} matches", "Result");
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