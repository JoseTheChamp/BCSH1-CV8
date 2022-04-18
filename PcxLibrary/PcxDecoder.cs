using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace PcxLibrary;

// Třída pro reprezentaci celé struktury PCX hlavičky
public class PcxHeader
{
    public byte Identifier { get; set; }
    public byte Version { get; set; }
    public byte Encoding { get; set; }
    public byte BitsPerPixel { get; set; }
    public ushort XStart { get; set; }
    public ushort YStart { get; set; }
    public ushort XEnd { get; set; }
    public ushort YEnd { get; set; }
    public ushort HorizontalResolution { get; set; }
    public ushort VerticalResolution { get; set; }
    public byte[/* 48 */]? Palette { get; set; }
    public byte Reserved1 { get; set; }
    public byte NumberOfBitPlanes { get; set; }
    public ushort BytesPerScanLine { get; set; }
    public ushort PaletteType { get; set; }
    public ushort HorizontalScreenSize { get; set; }
    public ushort VerticalScreenSize { get; set; }
    public byte[/* 54 */]? Reserverd2 { get; set; }

    public int Width => XEnd - XStart + 1;
    public int Height => YEnd - YStart + 1;
}

// Argumenty pro událost DecodingProgress
public class DecodingProgressEventArgs : EventArgs
{
    public int Progress { get; init; }
    public Image<Rgba32>? Image { get; init; } = null;
}

// Delegát pro událost DecodingProgress (informace o průběhu dekódování obrázku)
public delegate void DecodingProgressEventHandler(object sender, DecodingProgressEventArgs a);

// PCX dekodér
public class PcxDecoder
{
    // TODO: Vlastnost IsPcxFile musí vracet true pouze v případě, že podle header.Identifier se jedná o PCX obrázek.
    public bool IsPcxFile => header.Identifier == 10;

    // Událost slouží pro oznamování postupu při asynchronním dekódování obrázku.
    public event DecodingProgressEventHandler? DecodingProgress;
    // Vlastnost Image pro zpřístupnění obrázku po synchronním dékódování obrázku.
    public Image<Rgba32>? Image => image;
    // Vlastnost pro zpřístupnění hlavičky PCX obrázku
    public PcxHeader Header => header;

    // BinaryReader pro práci se vstupními daty
    private BinaryReader reader;
    // Dekódovaná hlavička obrázku
    private PcxHeader header;
    // Dekódovaný obrázek
    private Image<Rgba32>? image;

    // TODO: pro zpracování obrázků s rozšířenou 256 barevnou paletou bude třeba atribut pro zaznamenání palety

    public PcxDecoder(Stream stream)
    {
        reader = new BinaryReader(new BufferedStream(stream, 4096));
        header = new();
    }

    public void ReadHeader()
    {
        Debug.WriteLine("ReadHeader");
        header.Identifier = reader.ReadByte();
        if (header.Identifier == 10)
        {
            header.Version = reader.ReadByte();
            header.Encoding = reader.ReadByte();
            header.BitsPerPixel = reader.ReadByte();
            header.XStart = reader.ReadUInt16();
            header.YStart = reader.ReadUInt16();
            header.XEnd = reader.ReadUInt16();
            header.YEnd = reader.ReadUInt16();
            header.HorizontalResolution = reader.ReadUInt16();
            header.VerticalResolution = reader.ReadUInt16();
            header.Palette = reader.ReadBytes(48);
            header.Reserved1 = reader.ReadByte();
            header.NumberOfBitPlanes = reader.ReadByte();
            header.BytesPerScanLine = reader.ReadUInt16();
            header.PaletteType = reader.ReadUInt16();
            header.HorizontalScreenSize = reader.ReadUInt16();
            header.VerticalScreenSize = reader.ReadUInt16();
            header.Reserverd2 = reader.ReadBytes(54);
            Debug.WriteLine("ReadHeader - Success");
        }
        else {
            Debug.WriteLine("Zvoleny soubor nebyl .pcx");
        }
    }

    public void DecodeImageInForegroundThread()
    {
        image = new Image<Rgba32>(header.Width, header.Height);

        DecodeImageInternal();
    }

    public void DecodeImageInBackgroundThread()
    {
        image = new Image<Rgba32>(header.Width, header.Height);

        Thread workerThread = new(DecodeImageInternal)
        {
            IsBackground = true
        };
        workerThread.Start();
    }

    private void DecodeImageInternal()
    {
        Debug.WriteLine("InternalDecode");
        // TODO: Dokončete načítání obrazových dat do atributu image
        // - po načtení každého řádku obrazových dat vyvolejte událost DecodingProgress a předejte Progress (0-100 %)
        // - po dokončení načítání celého obrázku vyvolejte událost DecodingProgress (Progress = 100, Image = image)
        // - celá tato metoda pracuje synchronně a blokuje vlákno až do dokončení dekódování celého obrázku - nevytvářejte zde žádná vlákna, Tasky nebo jiné paralelizační objekty
        // - obrázek lze vyplnit pomocí: image[x, y] = new Rgba32(hodnotaRkanalu, hodnotaGkanalu, hodnotaBkanalu, 255);

        int ImageWidth = header.XEnd - header.XStart+1;
        int ImageHeight = header.YEnd - header.YStart+1;

        Debug.WriteLine("Vyska je " + ImageHeight);

        int ScanLineLength = header.NumberOfBitPlanes * header.BytesPerScanLine;
        int LinePaddingSize = ((header.BytesPerScanLine * header.NumberOfBitPlanes)*(8/header.BitsPerPixel))-((header.XEnd - header.XStart)+1);

        int[] dataR;
        int[] dataG;
        int[] dataB;
        int[] dataA;

        for (int i = 0; i < ImageHeight/2; i++)
        {
            Debug.WriteLine("Radek cislo: " + i);
            ScanLine(ImageWidth, out dataR); //ScanLineLength- LinePaddingSize
            ScanLine(ImageWidth, out dataG);
            ScanLine(ImageWidth, out dataB);
            ScanLine(ImageWidth, out dataA);
            for (int j = 0; j < ImageWidth; j++)
            {
                image[j, i] = new Rgba32(dataR[j],dataG[j],dataB[j],255); //
                //Debug.WriteLine(dataA[j]);
            }
            DecodingProgress.Invoke(this, new DecodingProgressEventArgs { Progress = i/ImageHeight});
        }
        Debug.WriteLine("DecodeSuccess");
        DecodingProgress.Invoke(this,new DecodingProgressEventArgs {Progress=100,Image=image });

        /*
        int ind = 0;
        while (true)
        {
            reader.ReadByte();
            ind++;
            if (ind % 1000 == 0) {
                Debug.WriteLine("Byte navic: " + ind);
            }
            
        }
        */
        // TODO: Pokud se jedná o obrázek s rozšířenou 256 barevnou paletou, tak před vlastním dekódováním obrazových dat je nutné provést načtení palety
    }

    private void ScanLine(int ScanLineLength,out int[] buffer) {
        int runCount = 0;
        int runValue = 0;
        byte byteA;
        int index = 0;
        int bufferSize = ScanLineLength;
        buffer = new int[ScanLineLength];
        int total = 0;

        do
        {
            byteA = reader.ReadByte();
            if ((byteA & 0xC0) == 0xC0)
            {
                runCount = byteA & 0x3F;
                runValue = reader.ReadByte();
            }
            else
            {
                runCount = 1;
                runValue = byteA;
            }
            for (total += runCount; runCount != 0 && index < bufferSize; runCount--, index++)
            {
                buffer[index] = runValue;
            }
        } while (index < bufferSize);
    }
}
