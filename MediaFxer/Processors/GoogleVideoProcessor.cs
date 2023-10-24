using MediaFixer.Model;
using Serilog;

namespace MediaFixer.Processors;

/// <summary>
/// Videos does not move, only archive since these cannot be uploaded to iCloud.
/// Extensions: ".mp4", ".m4v"
/// </summary>
internal class GoogleVideoProcessor : GoogleProcessor
{
    public override MediaType MediaType => MediaType.Video;

    public GoogleVideoProcessor(Options options, ILogger logger) : base(options, nameof(GoogleVideoProcessor), logger)
    {       
    }

    public override async Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
    {
        var metaFile = GetMetaFile(sourceFile, allFiles);
        if (metaFile == null)
        {
            Logger.Error($"No meta file found for {sourceFile.Name}");
            return new MediaProcessResult { Error = true };
        }

        var meta = await GetMetaData(metaFile);

        if (ScanMetaOnly)
        {
            return await GetMetaAndScanIt(metaFile, sourceFile.FullName);
        }

        if (!Options.ArchiveMedia)
        {
            return new MediaProcessResult { Skipped = true };
        }

        var videoArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Videos", sourceFile.Name);
        var metaArchiveFilePath = Path.Join(Options.ArchiveDirectory, "Metadata", $"{sourceFile.Name}.json");

        // Archive file
        File.Move(sourceFile.FullName, videoArchiveFilePath);
        if (meta != null)
        {
            File.Move(meta.FilePath, metaArchiveFilePath);
        }

        return new MediaProcessResult { Moved = true };
    }

}
