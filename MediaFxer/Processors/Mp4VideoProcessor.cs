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

        public override Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles)
        {
            var x = GetFileDesitnationPath(sourceFile);
            throw new NotImplementedException();
        }
    }
}
