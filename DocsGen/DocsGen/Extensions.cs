using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace DocsGen
{
    public static class Extensions
    {
        public static void TryConvertFromMarkdown(this IMiscellaneousClient miscClient, FileInfo fileInfo)
        {
            var htmlFilePath = fileInfo.FullName.Replace(".md", ".html");
            var htmlResponseTask =
                    miscClient.RenderRawMarkdown(File.ReadAllText(fileInfo.FullName));
            htmlResponseTask.ConfigureAwait(false);
            var htmlResponse = htmlResponseTask.Result;
            File.WriteAllText(htmlFilePath, htmlResponse);
        }
    }
}
