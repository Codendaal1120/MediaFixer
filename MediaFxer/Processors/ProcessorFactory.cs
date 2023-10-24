using MediaFixer.Model;
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
        public static IReadOnlyCollection<MediaProcessor> GetProcessors(Options options)
        {
            var processors = new List<MediaProcessor>();

            foreach (var processor in options.ImageProcessors)
            {
                var proc = GetImageProcessor(processor);
                if (proc != null) { processors.Add(proc); }
            }

            foreach (var processor in options.VideoProcessors)
            {
                var proc = GetImageProcessor(processor);
                if (proc != null) { processors.Add(proc); }
            }

            return processors;
        }

        private static MediaProcessor? GetImageProcessor(ProcessorOption processor) 
        {
            switch (processor.Name)
            {

                default: return null;
            }
        }

        private static MediaProcessor? GetVideoProcessor(ProcessorOption processor)
        {
            switch (processor.Name)
            {

                default: return null;
            }
        }
    }
}
