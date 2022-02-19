using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BinarySerializer;
using BinarySerializer.GBA;

namespace GBAC;

public class CompressedDataViewModel : BaseViewModel
{
    #region Constructor

    public CompressedDataViewModel(
        MessageService messageService, 
        BrowseService browseService, 
        FileDataProvider dataProvider, 
        CompressionViewModel compression, 
        uint offset, 
        uint compressedLength, 
        uint decompressedLength, 
        uint[] references)
    {
        Message = messageService;
        Browse = browseService;
        _dataProvider = dataProvider;
        Compression = compression;
        Offset = offset;
        CompressedLength = compressedLength;
        DecompressedLength = decompressedLength;
        References = references;
        InfoItems = new ObservableCollection<InfoItemViewModel>();

        CopyOffsetCommand = new RelayCommand(CopyOffset);
        CopyDataCommand = new RelayCommand(CopyData);
        ExportDataCommand = new RelayCommand(ExportData);
        ImportDataCommand = new RelayCommand(ImportData);

        LoadInfoItems();
    }

    #endregion

    #region Private Constants

    private const double DpiX = 96;
    private const double DpiY = 96;

    #endregion

    #region Private Fields

    private readonly FileDataProvider _dataProvider;
    private readonly object _loadLock = new();
    private uint _tilesPaletteOffset;

    #endregion

    #region Services

    private MessageService Message { get; }
    private BrowseService Browse { get; }

    #endregion

    #region Commands

    public ICommand CopyOffsetCommand { get; }
    public ICommand CopyDataCommand { get; }
    public ICommand ExportDataCommand { get; }
    public ICommand ImportDataCommand { get; }

    #endregion

    #region Public Properties

    public CompressionViewModel Compression { get; }
    public uint Offset { get; }
    public uint CompressedLength { get; private set; }
    public uint DecompressedLength { get; private set; }
    public uint[] References { get; }
    public int ReferencesCount => References.Length;

    public bool IsHighlighted { get; set; }

    public ObservableCollection<InfoItemViewModel> InfoItems { get; }

    public bool IsLoading { get; set; }
    public bool IsLoaded { get; set; }

    public byte[]? Data { get; set; }
    public string? DataString { get; set; }

    public ImageSource? PalettePreview { get; set; }

    public ImageSource? Tiles4Preview { get; set; }
    public ImageSource? Tiles8Preview { get; set; }
    public int TilesPreviewTileWidth { get; set; } = 2;
    public int TilesPreviewWidth => TilesPreviewTileWidth * GBAConstants.TileSize;
    public uint TilesPaletteOffset
    {
        get => _tilesPaletteOffset;
        set
        {
            bool wasValid = _dataProvider.IsOffsetValid(_tilesPaletteOffset);
            bool isValid = _dataProvider.IsOffsetValid(value);

            _tilesPaletteOffset = value;

            if (isValid || wasValid)
            {
                LoadTilePalette();
                LoadTilePreviews();
            }
        }
    }
    public Color[]? TilesPalette { get; set; }

    public ImageSource? Map8Preview { get; set; }
    public ImageSource? Map16Preview { get; set; }
    public int MapPreviewWidth { get; set; } = 100;

    #endregion

    #region Private Methods

    private static int GetStride(int width, PixelFormat format, int align = 0)
    {
        int stride = (int)(width / (8f / format.BitsPerPixel));

        if (align != 0)
        {
            if (stride % align != 0)
                stride += align - stride % align;
        }

        return stride;
    }

    private static int GetPaletteLength(int bpp) => (int)Math.Pow(2, bpp);

    private static Color[] CreateDummyPalette(int length, bool firstTransparent = true, int? wrap = null)
    {
        Color[] pal = new Color[length];

        wrap ??= length;

        if (firstTransparent)
            pal[0] = Colors.Transparent;

        for (int i = firstTransparent ? 1 : 0; i < length; i++)
        {
            byte val = (byte)((float)(i % wrap.Value) / (wrap.Value - 1) * 255);
            pal[i] = Color.FromRgb(val, val, val);
        }

        return pal;
    }

    private ImageSource? CreatePalettePreview()
    {
        try
        {
            if (Data == null)
                return null;

            byte[] data = Data;

            const int width = 16;
            const int maxHeight = 16;
            PixelFormat format = PixelFormats.Bgr555;

            int mod = data.Length % (16 * 2);

            if (mod != 0)
                data = data.Concat(new byte[16 * 2 - mod]).ToArray();

            int colorsCount = data.Length / 2;
            int height = Math.Min(colorsCount / width, maxHeight);

            return BitmapSource.Create(width, height, DpiX, DpiY, format, null, data, GetStride(width, format));
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when loading the palette preview");
            return null;
        }
    }

    private static void DrawTileTo8BPPImg(
        byte[] tileSet, 
        int tileSetOffset, 
        int tileSetBpp, 
        int paletteOffset, 
        bool flipX, bool flipY, 
        byte[] imgData, 
        int xPos, 
        int yPos, 
        int imgWidth)
    {
        float tileSetBppFactor = tileSetBpp / 8f;

        for (int y = 0; y < GBAConstants.TileSize; y++)
        {
            for (int x = 0; x < GBAConstants.TileSize; x++)
            {
                byte b = tileSet.ElementAtOrDefault((int)(tileSetOffset + (y * GBAConstants.TileSize + x) * tileSetBppFactor));

                if (tileSetBpp == 4)
                    b = (byte)BitHelpers.ExtractBits(b, 4, x % 2 == 0 ? 0 : 4);

                var sourceTileX = flipX ? GBAConstants.TileSize - x - 1 : x;
                var sourceTileY = flipY ? GBAConstants.TileSize - y - 1 : y;

                // Ignore transparent pixels
                if (b == 0)
                    continue;

                imgData[(yPos + sourceTileY) * imgWidth + xPos + sourceTileX] = (byte)(b + paletteOffset);
            }
        }
    }

    private ImageSource? CreateTilesPreview(int bpp, int width, IList<Color>? palette)
    {
        try
        {
            if (Data == null || width == 0)
                return null;

            // Get the format
            PixelFormat format = PixelFormats.Indexed8;
            float bppFactor = bpp / 8f;

            // Get the length of each tile in bytes
            int tileLength = (int)(GBAConstants.TileSize * GBAConstants.TileSize * bppFactor);

            // Calculate the height
            int height = (int)Math.Ceiling(Data.Length / (float)width / tileLength) * tileLength;

            if (height == 0)
                return null;

            // Get the tile dimensions
            int tilesWidth = width / GBAConstants.TileSize;
            int tilesHeight = height / GBAConstants.TileSize;
            int stride = GetStride(width, format);

            // Create a dummy palette if none is given
            palette ??= CreateDummyPalette(256, true, wrap: GetPaletteLength(bpp));

            // Get the palette
            BitmapPalette bmpPal = new BitmapPalette(palette);

            // Create a buffer for the image data
            byte[] imgData = new byte[width * height];

            // Enumerate every tile
            for (int tileY = 0; tileY < tilesHeight; tileY++)
            {
                int absTileY = tileY * GBAConstants.TileSize;

                for (int tileX = 0; tileX < tilesWidth; tileX++)
                {
                    int absTileX = tileX * GBAConstants.TileSize;

                    int tileIndex = tileY * tilesWidth + tileX;

                    int tileSetOffset = tileIndex * tileLength;

                    DrawTileTo8BPPImg(
                        tileSet: Data,
                        tileSetOffset: tileSetOffset,
                        tileSetBpp: bpp,
                        paletteOffset: 0,
                        flipX: false,
                        flipY: false,
                        imgData: imgData,
                        xPos: absTileX,
                        yPos: absTileY,
                        imgWidth: width);
                }
            }

            return BitmapSource.Create(width, height, DpiX, DpiY, format, bmpPal, imgData, stride);
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when loading the sprite preview");
            return null;
        }
    }

    private ImageSource? CreateMapPreview(int bpp, int width)
    {
        try
        {
            if (Data == null || width == 0)
                return null;

            byte[] data = Data;
            float bppFactor = bpp / 8f;

            PixelFormat format = bpp switch
            {
                8 => PixelFormats.Gray8,
                16 => PixelFormats.Gray16, // TODO: Perhaps find a better solution to this. If values are too low this will only show black.
                _ => throw new ArgumentOutOfRangeException(nameof(bpp), bpp, null)
            };

            int height = (int)((int)Math.Ceiling(Data.Length / (float)width) / bppFactor);
            int dataWidth = (int)(width * bppFactor);

            int mod = data.Length % dataWidth;

            if (height == 0)
                return null;

            if (mod != 0)
                data = data.Concat(new byte[dataWidth - mod]).ToArray();

            return BitmapSource.Create(width, height, DpiX, DpiY, format, null, data, GetStride(width, format));
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when loading the map preview");
            return null;
        }
    }

    #endregion

    #region Public Methods

    public void Load()
    {
        lock (_loadLock)
        {
            IsLoading = true;

            try
            {
                Data = _dataProvider.GetData(Offset, Compression.Encoder);
                DataString = Data.ToHexString(align: 16, maxLines: 16);

                if (Data.Length != 0)
                {
                    LoadPalettePreview();
                    LoadTilePalette();
                    LoadTilePreviews();
                    LoadMapPreviews();
                }

                IsLoaded = true;
            }
            catch (Exception ex)
            {
                Message.DisplayEception(ex, "An error occurred when loading the data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public void LoadPalettePreview()
    {
        PalettePreview = CreatePalettePreview();
    }

    public void LoadTilePalette()
    {
        if (!_dataProvider.IsOffsetValid(TilesPaletteOffset))
        {
            TilesPalette = null;
            return;
        }

        try
        {
            const int palLength = 256;

            byte[] data = _dataProvider.GetData(TilesPaletteOffset, palLength * 2);
            Color[] pal = new Color[palLength];

            for (int i = 0; i < palLength; i++)
            {
                if (i == 0)
                {
                    pal[i] = Colors.Transparent;
                    continue;
                }

                var value = data[i * 2 + 1] << 8 | data[i * 2];

                byte red = (byte)(BitHelpers.ExtractBits(value, 5, 0) / 31f * 255);
                byte green = (byte)(BitHelpers.ExtractBits(value, 5, 5) / 31f * 255);
                byte blue = (byte)(BitHelpers.ExtractBits(value, 5, 10) / 31f * 255);

                pal[i] = Color.FromRgb(red, green, blue);
            }

            TilesPalette = pal;
        }
        catch (Exception ex)
        {
            TilesPalette = null;
            Message.DisplayEception(ex, "An error occurred when loading the tile palette");
        }
    }

    public void LoadTilePreviews()
    {
        Tiles4Preview = CreateTilesPreview(4, TilesPreviewWidth, TilesPalette);
        Tiles8Preview = CreateTilesPreview(8, TilesPreviewWidth, TilesPalette);
    }

    public void LoadMapPreviews()
    {
        Map8Preview = CreateMapPreview(8, MapPreviewWidth);
        Map16Preview = CreateMapPreview(16, MapPreviewWidth);
    }

    public void LoadInfoItems()
    {
        InfoItems.Clear();

        InfoItems.Add(new InfoItemViewModel("Compression", Compression.DisplayName));
        InfoItems.Add(new InfoItemViewModel("Compressed Length", $"{CompressedLength:X}"));
        InfoItems.Add(new InfoItemViewModel("Decompressed Length", $"{DecompressedLength:X}"));
        InfoItems.Add(new InfoItemViewModel("References", String.Join(", ", References.Select(x => $"{x:X8}"))));
    }

    public void Unload()
    {
        lock (_loadLock)
        {
            Data = null;
            DataString = null;
            PalettePreview = null;
            IsLoaded = false;
        }
    }

    public byte[] GetData()
    {
        lock (_loadLock)
            return Data ?? _dataProvider.GetData(Offset, Compression.Encoder);
    }

    public void CopyOffset()
    {
        Clipboard.SetText($"{Offset:X8}");
    }

    public void CopyData()
    {
        try
        {
            Clipboard.SetText(GetData().ToHexString());
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when copying the data");
        }
    }

    public void ExportData()
    {
        string? filePath = Browse.SaveFile("Export raw data", $"{Offset:X8}.bin");

        if (filePath == null)
            return;

        try
        {
            File.WriteAllBytes(filePath, GetData());
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when exporting the data");
        }
    }

    public void ImportData()
    {
        string? filePath = Browse.OpenFile("Import raw data");

        if (filePath == null)
            return;

        try
        {
            // Read the bytes
            byte[] data = File.ReadAllBytes(filePath);

            // Compress the data
            byte[] compressedData = Compression.Encoder.EncodeBuffer(data);

            // Verify
            if (!Message.DisplayQuestion($"Importing this data will overwrite {compressedData.Length:X} bytes in the file. " +
                                         $"If this is bigger than the original compressed size then this might result in other " +
                                         $"data being overwritten. Continue?", "Confirm import"))
                return;

            _dataProvider.OverwriteData(Offset, compressedData);

            CompressedLength = (uint)compressedData.Length;
            DecompressedLength = (uint)data.Length;

            LoadInfoItems();
            Load();
        }
        catch (Exception ex)
        {
            Message.DisplayEception(ex, "An error occurred when importing the data");
        }
    }

    #endregion
}