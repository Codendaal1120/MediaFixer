using ExifLibrary;
using Serilog;
using MediaFixer.Model;
using System.Diagnostics;
using ImageMagick;
using ExifTag = ExifLibrary.ExifTag;

namespace MediaFixer.Processors;

internal class ImageProcessor : MediaProcessor
{
    public IReadOnlyCollection<string> _convertExtensions => new[] { ".heic", ".png" };
    public override IReadOnlyCollection<string> Extensions => new[] { ".heic", ".jpg", ".png" };    
    public override MediaType MediaType => MediaType.Image;

    public ImageProcessor(Options options) : base(options)
    {
    }

    private string GetFileDesitnationPath(FileInfo sourceFile)
    {
        if (!_convertExtensions.Contains(sourceFile.Extension.ToLower()))
        {
            return Path.Join(Options.Destination, sourceFile.Name);
        }

        return Path.Join(Options.Destination, sourceFile.Name).Replace(sourceFile.Extension, ".jpg");
    }

    public override (int skipped, int errors, int tagged, int moved, int convered) Process(FileInfo sourceFile, GoogleMeta? meta)
    {
        if (meta == null)
        {
            Log.Error($"Could not find meta data for file {sourceFile.Name}");
            return (1, 0, 0, 0, 0);
        }

        var tempFile = Path.Join(Options.TempDirectory, sourceFile.Name);
        var destinationFilePath = GetFileDesitnationPath(sourceFile);
        var imageArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Images", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        if (File.Exists(destinationFilePath))
        {
            if (!Options.OverWriteDestination)
            {
                Log.Warning($"{destinationFilePath} already exists, ignoring");
                return (1, 0, 0, 0, 0);
            }
        }

        try
        {
            // Create temp file
            File.Copy(sourceFile.FullName, tempFile, true);

            // tag file
            var tagged = TagImage(meta, tempFile);

            // convert image
            var converted = ConvertImage(tempFile);

            // move image
            File.Move(tempFile, destinationFilePath, true);

            // Archive file
            if (Options.ArchiveMedia)
            {
                File.Move(sourceFile.FullName, imageArchiveFilePath);
            }

            if (Options.ArchiveMeta) { File.Move(meta.FilePath, metaArchiveFilePath); }          

            return (0, 0, tagged, 1, converted);

        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Unable to process {sourceFile.FullName}");
            return (0, 1, 0, 0, 0);
        }
    }

    private int TagImage(GoogleMeta meta, string tempFile)
    {
        var img = ImageFile.FromFile(tempFile);
        var tagged = 0;

        // Set the DateTaken using ExifLib
        if (img.Properties.Get(ExifTag.DateTimeDigitized) == null)
        {
            tagged = 1;
            img.Properties.Set(ExifTag.DateTimeDigitized, GetPropertyDateTaken(meta, tempFile));
        }

        // set GPS properties
        var existingLat = img.Properties.Get(ExifTag.GPSLatitude);
        var existingLng = img.Properties.Get(ExifTag.GPSLongitude);

        if (existingLat == null)
        {
            tagged = 1;
            img.Properties.Set(ExifTag.GPSLatitude, meta.GeoDataExif.Longitude);
            img.Properties.Set(ExifTag.GPSLongitude, meta.GeoDataExif.Latitude);
        }

        img.Save(tempFile);

        return tagged;
    }

    private int ConvertImage(string tempFile)
    {
        var fi = new FileInfo(tempFile);

        if (!_convertExtensions.Contains(fi.Extension.ToLower()))
        {
            return 0;
        }

        // Read image from file
        using (var image = new MagickImage(tempFile))
        {
            // PNG files will loose Exif data
            var newName = tempFile.Replace(fi.Extension, ".jpg");
            image.Format = MagickFormat.Jpeg;
            image.Write(newName);
            return 1;
        }
    }

    private DateTime GetPropertyDateTaken(GoogleMeta meta, string file)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (meta.PhotoTakenTime.Timestamp != null)
        {
            var unixtime = Convert.ToInt64(meta.PhotoTakenTime.Timestamp);
            return epoch.AddSeconds(unixtime);
        }

        if (meta.CreationTime.Timestamp != null)
        {
            var unixtime = Convert.ToInt64(meta.CreationTime.Timestamp);

            return epoch.AddSeconds(unixtime);
        }

        Log.Error($"Could not determine date taken for {file}");
        return epoch;
    }
}
