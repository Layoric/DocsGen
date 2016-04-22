using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocsGen.ServiceModel;

namespace DocsGen.ServiceInterface
{
    public static class RequestExtensions
    {
        public static bool IsPushEvent(this GitHubEvent gitHubEvent)
        {
            return gitHubEvent.Commits.Count > 0;
        }

        public static bool IsGollumEvent(this GitHubEvent gitHubEvent)
        {
            return gitHubEvent.Pages.Count > 0;
        }

        public static bool HasMarkdownChanges(this GitHubEvent gitHubEvent)
        {
            if (gitHubEvent.IsGollumEvent()) return true;
            return gitHubEvent.Commits.Any(x =>
                    x.Modified.Any(y => y.ToLower().EndsWith(".md")) ||
                    x.Added.Any(y => y.ToLower().EndsWith(".md")) || 
                    x.Removed.Any(y => y.ToLower().EndsWith(".md"))
                );
        }
    }
}
