using Serilog;

namespace MediaFixer.Model;

internal class RunMetrics
{
    public int FilesChecked { get; set; }
    public int ImagesTagged { get; set; }
    public int ImagesConverted { get; set; }
    public int ImagesMoved { get; set; }
    public int VideosMoved { get; set; }
    public int VideosConverted { get; set; }
    public int VideosTagged { get; set; }
    public int FilesSkipped { get; set; }
    public int Errors { get; set; }

    public void Print()
    {
        Log.Information($"----> {FilesChecked} files checked");
        Log.Information($"----> {FilesSkipped} files skipped");
        Log.Information($"----> {Errors} errors");
        Log.Information($"----> ** Images **");
        Log.Information($"----> {ImagesTagged} images tagged");
        Log.Information($"----> {ImagesConverted} images converted");
        Log.Information($"----> {ImagesMoved} images moved");
        Log.Information($"----> ** Videos **");
        Log.Information($"----> {VideosTagged} videos tagged");
        Log.Information($"----> {VideosConverted} videos converted");
        Log.Information($"----> {VideosMoved} videos moved");
    }
}
