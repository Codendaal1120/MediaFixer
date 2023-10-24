using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaFixer
{
    internal static class JsonReader
    {
        public static async Task<T> ReadFileData<T>(string file)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            using var sr = new StreamReader(file);
            var content = await sr.ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(content, options)!;
        }
    }
}
