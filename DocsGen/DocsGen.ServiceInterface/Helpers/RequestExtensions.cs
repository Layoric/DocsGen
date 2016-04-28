using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocsGen.ServiceModel;

namespace DocsGen.ServiceInterface.Helpers
{
    public static class RequestExtensions
    {
        public static bool HasMarkdownChanges(this GitHubCommitEvent gitHubEvent)
        {
            return gitHubEvent.Commits.Any(x =>
                    x.Modified.Any(y => y.ToLower().EndsWith(".md")) ||
                    x.Added.Any(y => y.ToLower().EndsWith(".md")) ||
                    x.Removed.Any(y => y.ToLower().EndsWith(".md"))
                );
        }

        public static bool IsPushEvent(this GitHubCommitEvent gitHubEvent)
        {
            return gitHubEvent.Commits.Count > 0;
        }

        public static bool IsGollumEvent(this GitHubGollumEvent gitHubEvent)
        {
            return gitHubEvent.Pages.Count > 0;
        }
    }
}
