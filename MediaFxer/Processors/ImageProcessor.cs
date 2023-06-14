using ExifLibrary;
using Serilog;
using MediaFixer.Model;

namespace MediaFixer.Processors;

internal class ImageProcessor : MediaProcessor
{

    public override IReadOnlyCollection<string> Extensions => new[] { ".jpg", ".png" };

    public ImageProcessor(Options options) : base(options)
    {
    }


    public override (int skipped, int errors, int tagged, int moved, int convered) Process(FileInfo sourceFile, GoogleMeta? meta)
    {
        if (meta == null)
        {
            Log.Error($"Could not find meta data for file {sourceFile.Name}");
            return (1, 0, 0, 0, 0);
        }

        var destinationFilePath = Path.Join(Options.Destination, sourceFile.Name);
        var imageArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Images", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");
        var tagged = 0;

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

            var img = ImageFile.FromFile(sourceFile.FullName);

            // Set the DateTaken using ExifLib
            if (img.Properties.Get(ExifTag.DateTimeDigitized) == null)
            {
                tagged = 1;
                img.Properties.Set(ExifTag.DateTimeDigitized, GetPropertyDateTaken(meta, sourceFile.FullName));
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

            img.Save(destinationFilePath);

            // Archive file
            if (Options.ArchiveMedia)
            {
                File.Move(sourceFile.FullName, imageArchiveFilePath);
            }
            if (Options.ArchiveMeta) { File.Move(meta.FilePath, metaArchiveFilePath); }

            // TODO convert file

            return (0, 0, tagged, 1, 0);

        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Unable to process {sourceFile.FullName}");
            return (0, 1, 0, 0, 0);
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
