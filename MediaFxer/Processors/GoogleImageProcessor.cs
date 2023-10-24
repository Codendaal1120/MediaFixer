using ExifLibrary;
using Serilog;
using MediaFixer.Model;
using ImageMagick;
using ExifTag = ExifLibrary.ExifTag;

namespace MediaFixer.Processors;

/// <summary>
/// Convert extensions: ".heic", ".png"
/// Extensions: ".heic", ".jpg", ".png"
/// </summary>
internal class GoogleImageProcessor : GoogleProcessor
{
    public IReadOnlyCollection<string> _convertExtensions;
    public override MediaType MediaType => MediaType.Image;

    public GoogleImageProcessor(Options options, ILogger logger) : base(options, nameof(GoogleImageProcessor), logger)
    {

        if (!ProcessorOptions.Options.TryGetValue("ConvertExtensions", out var configConvertExtensions))
        {
            throw new ArgumentException($"Missing or invalid config for ConvertExtensions");
        }

        var split = configConvertExtensions.Split(';');
        _convertExtensions = split;
    }

    public override async Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
    {
        var metaFile = GetMetaFile(sourceFile, allFiles);
        if (metaFile == null)
        {
            Log.Error($"Could not find meta data for file {sourceFile.Name}");
            return new MediaProcessResult() { Error = true };
        }
        var meta = await GetMetaData(metaFile);

        var tempFile = Path.Join(Options.TempDirectory, sourceFile.Name);
        var destinationFilePath = GetFileDesitnationPath(sourceFile, GetNewExtension(sourceFile));
        var imageArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Images", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        if (File.Exists(destinationFilePath))
        {
            if (!Options.OverWriteDestination)
            {
                Log.Warning($"{destinationFilePath} already exists, ignoring");
                return new MediaProcessResult() { Skipped = true };
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

            if (ArchiveMeta) { File.Move(meta.FilePath, metaArchiveFilePath); }

            return new MediaProcessResult() { Tagged = tagged, Converted = converted, Moved = true };

        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Unable to process {sourceFile.FullName}");
            return new MediaProcessResult() { Error = true };
        }
    }

    private string? GetNewExtension(FileInfo sourceFile)
    {
        if (!_convertExtensions.Contains(sourceFile.Extension.ToLower()))
        {
            return null;            
        }

        return ".jpg";
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

    private bool ConvertImage(string tempFile)
    {
        var fi = new FileInfo(tempFile);

        if (!_convertExtensions.Contains(fi.Extension.ToLower()))
        {
            return false;
        }

        // Read image from file
        using (var image = new MagickImage(tempFile))
        {
            // PNG files will loose Exif data
            var newName = tempFile.Replace(fi.Extension, ".jpg");
            image.Format = MagickFormat.Jpeg;
            image.Write(newName);
            return true;
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
