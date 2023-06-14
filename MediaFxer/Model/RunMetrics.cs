namespace MediaFixer.Model;

internal class RunMetrics
{
    public int FilesChecked { get; set; }
    public int ImagesProcessed { get; set; }
    public int VideosProcessed { get; set; }
    public int FilesSkipped { get; set; }
    public int Errors { get; set; }
}
