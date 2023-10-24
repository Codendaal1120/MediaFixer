using MediaFixer.Model;
using Serilog;

namespace MediaFixer.Processors;

/// <summary>
/// Images needs to be tagged, converted (HEIC is not supported by iCloud) 
/// moved to the uplload and archived for NAS storage
/// </summary>
internal abstract class MediaProcessor
{ 
    public abstract MediaType MediaType { get; }
    public bool HasInitilized { get; protected set; }
    public IReadOnlyCollection<string> Extensions { get; init; }

    protected ILogger Logger { get; init; }
    protected ProcessorOption ProcessorOptions { get; init; }
    protected readonly Options Options;

    protected MediaProcessor(Options options, string processorName, ILogger logger)
    {
        Options = options;
        Logger = logger;

        var processorConfig = options.ImageProcessors.FirstOrDefault(x => x.Name.Equals(processorName));

        if (processorConfig == null)
        {
            processorConfig = options.VideoProcessors.FirstOrDefault(x => x.Name.Equals(processorName));
        }

        if (processorConfig == null)
        {
            throw new ArgumentNullException(processorName);
        }

        ProcessorOptions = processorConfig;

        Extensions = processorConfig.Extensions;
    }

    /// <summary>
    /// 1. Check destination of already exists
    /// 2. Create a copy (we will edit the copy)
    /// 3. Edit tags
    /// </summary>
    public abstract Task<MediaProcessResult> Process(FileInfo sourceFile, IReadOnlyCollection<FileInfo> allFiles);

    public abstract Task Initilize();

    protected string GetFileDesitnationPath(FileInfo sourceFile, string? newExtension = null)
    {
        var dest = Path.Join(Options.Destination, sourceFile.Name);

        if (Options.CarrySubfolder && sourceFile.Directory != null && !sourceFile.Directory.FullName.Equals(Options.Destination, StringComparison.InvariantCultureIgnoreCase))
        {
            dest = Path.Join(Options.Destination, sourceFile.Directory.Name, sourceFile.Name);
        }

        if (!string.IsNullOrEmpty(newExtension))
        {
            dest.Replace(dest, newExtension);
        }

        return dest;
    }
}
