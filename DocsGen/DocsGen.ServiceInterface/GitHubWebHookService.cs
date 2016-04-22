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

        public void Any(GitHubEvent request)
        {
            logger.Debug("WebHook received.");

            logger.Debug("Processing update for FullName: {0}\nName:{1}".Fmt(request.Repository.FullName, request.Repository.Name));
            var docsRepoName = AppSettings.GetString("DocsRepoOwner") + "/" + AppSettings.GetString("DocsRepoName");

            if (request.IsPushEvent() && request.HasMarkdownChanges())
            {
                logger.Debug("Comparing {0} to {1}".Fmt(request.Repository.FullName, docsRepoName));
                if (request.Repository.FullName == docsRepoName)
                    Task.Run(() =>
                    {
                        UpdateDocsRepository();
                    });
            }

            if (request.IsGollumEvent())
            {
                var wikiRepoName = AppSettings.GetString("WikiRepoOwner") + "/" + AppSettings.GetString("WikiRepoName");
                logger.Debug("Comparing {0} to {1}".Fmt(request.Repository.FullName,wikiRepoName));
                if (request.Repository.FullName == wikiRepoName)
                    Task.Run(() =>
                    {
                        UpdateFromWikiLocalRepo();
                    });
            }

            logger.Debug("GitHub Webhook received.\n");
        }

        private void UpdateDocsRepository()
        {
            var localRepoPath = AppSettings.GetString("LocalDocsRepoLocation");
            GitHelpers.PullRepo(localRepoPath);
            MiscellaneousClient.StartHtmlUpdate(AppSettings);
            GitHelpers.CommitChangesToDocs(AppSettings);
        }

        private void UpdateFromWikiLocalRepo()
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
}
