using MediaFixer.Model;
using Serilog;

namespace MediaFixer.Processors
{
    /// <summary>
    /// Converts the video file to MP$ (for icloud). The converted file is also tagged
    /// </summary>
    internal class Mp4VideoProcessor : MediaProcessor
    {
        public Mp4VideoProcessor(Options options, ILogger logger) : base(options, nameof(Mp4VideoProcessor), logger)
        {
        }

        public override MediaType MediaType => MediaType.Video;

        public override Task Initilize()
        {
            HasInitilized = true;
            return Task.CompletedTask;
        }

        public override async Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
        {
            //var metaFile = GetMetaFile(sourceFile, allFiles);
            //if (metaFile == null)
            //{
            //    Log.Error($"Could not find meta data for file {sourceFile.Name}");
            //    return new MediaProcessResult() { Error = true };
            //}
            //var meta = await GetMetaData(metaFile);

            var tempFile = Path.Join(Options.TempDirectory, sourceFile.Name);
            var destinationFilePath = GetFileDesitnationPath(sourceFile, ".mp4");
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
                var tagged = TagVideo(tempFile);

                // convert image
                var converted = ConvertVideo(tempFile);

                // move image
                File.Move(tempFile, destinationFilePath, true);

                // Archive file
                if (Options.ArchiveMedia)
                {
                    File.Move(sourceFile.FullName, imageArchiveFilePath);
                }

               // if (ArchiveMeta) { File.Move(meta.FilePath, metaArchiveFilePath); }

                return new MediaProcessResult() { Tagged = tagged, Converted = converted, Moved = true };

            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to process {sourceFile.FullName}");
                return new MediaProcessResult() { Error = true };
            }
        }

        private int TagVideo(string tempFile)
        {
            return 0;
        }

        private bool ConvertVideo(string tempFile)
        {
            return false;
        }
    }
}
