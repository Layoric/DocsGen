using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocsGen.ServiceInterface.Helpers;
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

        public IMiscellaneousClient MiscellaneousClient { get; set; }
        public IAppSettings AppSettings { get; set; }

        public void Any(GitHubPushEvent request)
        {
            if (request.HeadCommit == null) logger.Debug("HeadCommit is null");
            if (request.HeadCommit != null && request.HeadCommit.Modified == null) logger.Debug("Modified is null");
            var filesToUpdate = AddedOrChangedMarkDownFiles(request);
            var filesToRemove = RemovedMarkDownFiles(request);

            var docsRepoName = AppSettings.GetString("DocsRepoOwner") + "/" + AppSettings.GetString("DocsRepoName");
            var wikiRepoName = AppSettings.GetString("WikiRepoOwner") + "/" + AppSettings.GetString("WikiRepoName");

            if(request.Repository.FullName == docsRepoName)
                Task.Run(() =>
                {
                    UpdateDocsRepository(filesToUpdate, filesToRemove);
                });

            if(request.Repository.FullName == wikiRepoName)
                Task.Run(() =>
                {
                    UpdateFromWikiLocalRepo(filesToUpdate, filesToRemove);
                });

            logger.Debug("GitHub Webhook received.\n\n" + filesToUpdate.Join(";"));
        }

        private void UpdateDocsRepository(List<string> filesToUpdate, List<string> filesToRemove)
        {
            if (filesToUpdate.Count > 0 || filesToRemove.Count > 0)
            {
                var localRepoPath = AppSettings.GetString("LocalDocsRepoLocation");
                GitHelpers.PullRepo(localRepoPath);
                MiscellaneousClient.StartHtmlUpdate(AppSettings);
                GitHelpers.CommitChangesToDocs(AppSettings);
            }
        }

        private void UpdateFromWikiLocalRepo(List<string> filesToUpdate, List<string> filesToRemove)
        {
            if (filesToUpdate.Count > 0 || filesToRemove.Count > 0)
            {
                var localWikiPath = AppSettings.GetString("LocalWikiRepoLocation");
                GitHelpers.PullRepo(localWikiPath);

                var localRepoPath = AppSettings.GetString("LocalDocsRepoLocation");
                var localRepoDirInfo = new DirectoryInfo(localRepoPath);
                var wikiDirInfo = new DirectoryInfo(localWikiPath);

                var wikiFiles = wikiDirInfo.GetFiles("*.md", SearchOption.AllDirectories).ToList();

                wikiFiles.ForEach(wikiFile =>
                {
                    string relativePath = wikiFile.FullName.Replace(wikiDirInfo.FullName + "\\", "");
                    string destPath = Path.Combine(localRepoDirInfo.FullName + "\\wiki", relativePath);
                    logger.Debug("Copying file from {0} \n To: \n {1}".Fmt(wikiFile.FullName, destPath));
                    File.Copy(wikiFile.FullName, destPath, true);
                });

                WikiDocumentMappings.MarkdownMappings.ForEach((key, val) =>
                {
                    var sourcePath = Path.Combine(localRepoDirInfo.FullName, key.Replace("/", "\\"));
                    var destPath = Path.Combine(localRepoDirInfo.FullName, val.Replace("/", "\\"));
                    File.Copy(sourcePath, destPath, true);
                });

                MiscellaneousClient.StartHtmlUpdate(AppSettings);
                GitHelpers.CommitChangesToDocs(AppSettings);
            }
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
