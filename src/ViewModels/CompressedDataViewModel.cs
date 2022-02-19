using System;
using System.Collections.Generic;
using System.Linq;
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
        CompressedDataProvider dataProvider, 
        CompressionViewModel compression, 
        uint offset, 
        uint compressedLength, 
        uint decompressedLength, 
        uint[] references)
    {
        Message = messageService;
        _dataProvider = dataProvider;
        Compression = compression;
        Offset = offset;
        CompressedLength = compressedLength;
        DecompressedLength = decompressedLength;
        References = references;

        InfoItems = new InfoItemViewModel[]
        {
            new InfoItemViewModel("Compression", Compression.DisplayName),
            new InfoItemViewModel("Compressed Length", $"{CompressedLength:X}"),
            new InfoItemViewModel("Decompressed Length", $"{DecompressedLength:X}"),
            new InfoItemViewModel("References", String.Join(", ", References.Select(x => $"{x:X8}"))),
        };
    }

    #endregion

    #region Private Constants

    private const double DpiX = 96;
    private const double DpiY = 96;

    #endregion

    #region Private Fields

    private readonly CompressedDataProvider _dataProvider;
    private readonly object _loadLock = new();

    #endregion

    #region Services

    private MessageService Message { get; }

    #endregion

    #region Public Properties

    public CompressionViewModel Compression { get; }
    public uint Offset { get; }
    public uint CompressedLength { get; }
    public uint DecompressedLength { get; }
    public uint[] References { get; }
    public int ReferencesCount => References.Length;

    public bool IsHighlighted { get; set; }

    public InfoItemViewModel[] InfoItems { get; }

    public bool IsLoading { get; set; }
    public bool IsLoaded { get; set; }

    public byte[]? Data { get; set; }
    public string? DataString { get; set; }

    public ImageSource? PalettePreview { get; set; }

    public ImageSource? Tiles4Preview { get; set; }
    public ImageSource? Tiles8Preview { get; set; }
    public int TilesPreviewTileWidth { get; set; } = 2;
    public int TilesPreviewWidth => TilesPreviewTileWidth * GBAConstants.TileSize;

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

    private static BaseColor[] CreateDummyPalette(int length, bool firstTransparent = true, int? wrap = null)
    {
        BaseColor[] pal = new BaseColor[length];

        wrap ??= length;

        if (firstTransparent)
            pal[0] = BaseColor.Clear;

        for (int i = firstTransparent ? 1 : 0; i < length; i++)
        {
            float val = (float)(i % wrap.Value) / (wrap.Value - 1);
            pal[i] = new CustomColor(val, val, val);
        }

        return pal;
    }

    private static IList<Color> ConvertColors(IEnumerable<BaseColor> colors, int bpp, bool trimPalette)
    {
        int wrap = GetPaletteLength(bpp);

        var c = colors.Select((x, i) => Color.FromArgb(
            a: (byte)(i % wrap == 0 ? 0 : 255),
            r: (byte)(x.Red * 255),
            g: (byte)(x.Green * 255),
            b: (byte)(x.Blue * 255))).ToArray();

        if (trimPalette && c.Length >= wrap)
            c = c.Take(wrap).ToArray();

        return c;
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

    private ImageSource? CreateTilesPreview(int bpp, int width, IList<BaseColor>? palette)
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
            if (palette?.Any() != true)
                palette = CreateDummyPalette(256, true, wrap: GetPaletteLength(bpp));

            // Get the palette
            BitmapPalette bmpPal = new BitmapPalette(ConvertColors(palette, bpp, false));

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
                16 => PixelFormats.Gray16,
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
                    LoadTilePreviews();
                    LoadMapPreviews();
                }

                IsLoaded = true;
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

    public void LoadTilePreviews()
    {
        // TODO: Allow a palette to be specified from an offset
        Tiles4Preview = CreateTilesPreview(4, TilesPreviewWidth, null);
        Tiles8Preview = CreateTilesPreview(8, TilesPreviewWidth, null);
    }

    public void LoadMapPreviews()
    {
        Map8Preview = CreateMapPreview(8, MapPreviewWidth);
        Map16Preview = CreateMapPreview(16, MapPreviewWidth);
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

    #endregion
}