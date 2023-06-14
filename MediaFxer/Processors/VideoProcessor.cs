using MediaFixer.Model;

namespace MediaFixer.Processors;

/// <summary>
/// Videos does not move, only archive since these cannot be uploaded to iCloud.
/// </summary>
internal class VideoProcessor : MediaProcessor
{
    public override IReadOnlyCollection<string> Extensions => new[] { ".mp4", ".m4v" };

    public VideoProcessor(Options options) : base(options)
    {
    }

    public override (int skipped, int errors, int tagged, int moved, int convered) Process(FileInfo sourceFile, GoogleMeta? meta)
    {
        if (!Options.ArchiveMedia)
        {
            return (1, 0, 0, 0, 0);
        }

        var videoArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Videos", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        // Archive file
        File.Move(sourceFile.FullName, videoArchiveFilePath);
        if (meta != null)
        {
            File.Move(meta.FilePath, metaArchiveFilePath);
        }

        return (0, 0, 0, 1, 0);
    }
}
