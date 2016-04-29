using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocsGen.ServiceInterface.Helpers;
using DocsGen.ServiceModel;
using Octokit;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using ServiceStack.Text;
using Repository = LibGit2Sharp.Repository;

namespace DocsGen.ServiceInterface
{
    public class GitHubWebHookService : Service
    {
        private readonly ILog logger = LogManager.GetLogger(typeof(MyServices));

        public IMiscellaneousClient MiscellaneousClient { get; set; }
        public IAppSettings AppSettings { get; set; }

        private const string githubRepoUrlFmt = "https://github.com/{0}.git";

        public void Any(GitHubCommitEvent request)
        {
            logger.Debug("WebHook received.");
            logger.Debug("Full request received: \n" + request.Dump());
            // Avoid loop from commiting changes itself.
            if (request.Pusher.Email == GitHelpers.BotEmail)
            {
                logger.Debug("Push event from " + GitHelpers.BotEmail + ". Ignoring.");
                return;
            }
            logger.Debug("Processing update for repository - FullName: {0}".Fmt(request.Repository.FullName));

            if (!request.IsPushEvent() || !request.HasMarkdownChanges())
            {
                logger.Debug("Invalid request or no Markdown changes detected. Skipping update.");
                return;
            }

            logger.Debug("Markdown changes detected, pull changes and updating.");
            var repoGitHubUrl = githubRepoUrlFmt.Fmt(request.Repository.FullName);
            var localRepoBasePath = AppSettings.GetString("LocalRepositoryBasePath");
            Task.Run(() =>
            {
                try
                {
                    string owner = request.Repository.FullName.Split('/')[0];
                    string repoName = request.Repository.FullName.Split('/')[1];
                    string localPath = Path.Combine(localRepoBasePath,owner + "\\" + repoName);
                    if (!GitHelpers.HasLocalRepo(localRepoBasePath, owner, repoName))
                    {

                        if (!Directory.Exists(localPath))
                            Directory.CreateDirectory(localPath);
                        Repository.Clone(repoGitHubUrl, localPath);
                    }
                    else
                    {
                        GitHelpers.PullRepo(localPath);
                    }

                    MiscellaneousClient.StartHtmlUpdate(localPath);
                    var ghUserId = AppSettings.GetString("GitHubUsername");
                    var ghToken = AppSettings.GetString("GitHubToken");
                    GitHelpers.CommitAndPushToOrigin(localPath, request.Repository.FullName, ghUserId, ghToken);
                }
                catch (Exception e)
                {
                    logger.Error("Failed to update from webhook.",e);
                }
            });
        }

        public void Any(GitHubGollumEvent request)
        {
            logger.Debug("Wiki WebHook received.");
            logger.Debug("Processing update for FullName: {0}\nName:{1}".Fmt(request.Repository.FullName, request.Repository.Name));

            if (request.IsGollumEvent())
            {
                var wikiRepoName = AppSettings.GetString("WikiRepoOwner") + "/" + AppSettings.GetString("WikiRepoName");
                logger.Debug("Comparing {0} to {1}".Fmt(request.Repository.FullName, wikiRepoName));
                if (request.Repository.FullName == wikiRepoName)
                    Task.Run(() =>
                    {
                        UpdateFromWikiLocalRepo();
                    });
            }
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

            MiscellaneousClient.StartHtmlUpdate(localRepoPath);
            GitHelpers.CommitChangesToDocs(AppSettings);
        }
    }
}
