using MediaFixer.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaFixer.Processors
{
    internal static class ProcessorFactory
    {
        public static IReadOnlyCollection<MediaProcessor> GetProcessors(Options options, ILogger logger)
        {
            var processors = new List<MediaProcessor>();

            foreach (var processor in options.ImageProcessors)
            {
                var proc = GetImageProcessor(options, logger, processor);
                if (proc != null) { processors.Add(proc); }
            }

            foreach (var processor in options.VideoProcessors)
            {
                var proc = GetVideoProcessor(options, logger, processor);
                if (proc != null) { processors.Add(proc); }
            }

            return processors;
        }

        private static MediaProcessor? GetImageProcessor(Options options, ILogger logger, ProcessorOption processor) 
        {
            switch (processor.Name)
            {
                case nameof(GoogleImageProcessor):
                    return new GoogleImageProcessor(options, logger);

                default: return null;
            }
        }

        private static MediaProcessor? GetVideoProcessor(Options options, ILogger logger, ProcessorOption processor)
        {
            switch (processor.Name)
            {
                case nameof(GoogleVideoProcessor):
                    return new GoogleVideoProcessor(options, logger);

                case nameof(Mp4VideoProcessor):
                    return new Mp4VideoProcessor(options, logger);

                default: return null;
            }
        }
    }
}
