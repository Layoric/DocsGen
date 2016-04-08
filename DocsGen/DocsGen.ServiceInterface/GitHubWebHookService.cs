using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocsGen.ServiceModel;
using Octokit;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;

namespace DocsGen.ServiceInterface
{
    public class GitHubWebHookService : Service
    {
        private readonly ILog logger = LogManager.GetLogger(typeof(MyServices));
        private static string gitHubRawBaseUrl = "https://raw.githubusercontent.com/{0}/{1}/master/";

        public IMiscellaneousClient MiscellaneousClient { get; set; }
        public IAppSettings AppSettings { get; set; }

        public void Any(GitHubPushEvent request)
        {
            if (request.HeadCommit == null) logger.Debug("HeadCommit is null");
            if (request.HeadCommit != null && request.HeadCommit.Modified == null) logger.Debug("Modified is null");
            var filesToUpdate = AddedOrChangedMarkDownFiles(request);
            var filesToRemove = RemovedMarkDownFiles(request);
            Task.Run(() =>
            {
                filesToUpdate.ForEach(fileName =>
                {
                    try
                    {
                        var repoOwner = AppSettings.GetString("DocsRepoOwner");
                        var repoName = AppSettings.GetString("DocsRepoName");
                        var localRepoBasePath = AppSettings.GetString("DocsBasePath");
                        var fullPath = Path.Combine(localRepoBasePath, fileName.ReplaceAll("/", "\\"));
                        var fileInfo = new FileInfo(fullPath);
                        if (fileInfo.Directory == null)
                        {
                            return;
                        }
                        var rawUrl = gitHubRawBaseUrl.Fmt(repoOwner,repoName) + fileName;
                        var contents = rawUrl.GetStringFromUrl("text/plain");
                        File.WriteAllText(fullPath, contents);
                        var renderTask = MiscellaneousClient.RenderRawMarkdown(contents);
                        renderTask.ConfigureAwait(false);
                        renderTask.ContinueWith(res =>
                        {
                            File.WriteAllText(fullPath.ToLower().Replace(".md", ".html"), res.Result);
                        });
                    }
                    catch (Exception e)
                    {
                        logger.Error("Error converting changes", e);
                        throw;
                    }

                });
            });
            logger.Debug("GitHub Webhook received.\n\n" + filesToUpdate.Join(";"));
        }

        private List<string> AddedOrChangedMarkDownFiles(GitHubPushEvent request)
        {
            List<string> result = new List<string>();
            if (request.HeadCommit == null)
                return result;

            result.AddRange(request.HeadCommit.Added.Where(x => x.ToLower().EndsWith(".md")));
            result.AddRange(request.HeadCommit.Modified.Where(x => x.ToLower().EndsWith(".md")));
            return result;
        }

        private List<string> RemovedMarkDownFiles(GitHubPushEvent request)
        {
            return request.HeadCommit?.Removed.Where(x => x.ToLower().EndsWith(".md")).ToList() ?? new List<string>();
        }
    }
}
