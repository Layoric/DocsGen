using System;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Octokit;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace DocsGen.ServiceInterface.Helpers
{
    public static class GitHelpers
    {
        private static ILog Logger => LogManager.GetLogger(typeof(MyServices));

        public static void CommitChangesToDocs(IAppSettings appSettings)
        {
            var ownerName = appSettings.GetString("WikiRepoOwner");
            var repoName = appSettings.GetString("WikiRepoName");
            var localRepoPath = appSettings.GetString("LocalDocsRepoLocation");
            var localRepoWikiPath = Path.Combine(localRepoPath, "wiki");
            var repo = new Repository(localRepoPath);
            var signature = new Signature("ServiceStackDocsBot", "docsbot@servicestack.net", DateTimeOffset.UtcNow);
            repo.Stage(localRepoWikiPath, new StageOptions());
            Logger.Debug("Staging changes to Docs");
            var hasChanges = repo.RetrieveStatus(new StatusOptions()).IsDirty;
            Logger.Debug(hasChanges ? "Changes detected!" : "No changes.");
            if (hasChanges)
            {
                try
                {
                    Logger.Debug("Changes being commited: " + repo.RetrieveStatus(new StatusOptions()).Staged.ToList().Select(x => x.FilePath + "\n"));
                    repo.Commit(
                    "Lastest wiki migration from {0}/{1}.".Fmt(ownerName, repoName),
                    signature, signature,
                    new CommitOptions());
                    PushOptions options = new PushOptions();
                    var ghUserId = appSettings.GetString("GitHubUsername");
                    var ghToken = appSettings.GetString("GitHubToken");
                    options.CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials()
                        {
                            Username = ghUserId,
                            Password = ghToken
                        };
                    repo.Network.Push(repo.Branches["master"], options);
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to commit changes.", e);
                    throw;
                }
                
            }
        }

        public static void UpdateLocalRepo(IAppSettings appSettings)
        {
            var ownerName = appSettings.GetString("DocsRepoOwner");
            var repoName = appSettings.GetString("DocsRepoName");
            var networkRepoUrl = "https://github.com/" + ownerName + "/" + repoName + ".git";
            var localRepoPath = appSettings.GetString("LocalDocsRepoLocation");
            try
            {
                Repository.Clone(networkRepoUrl, localRepoPath, new CloneOptions());
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to clone docs. Trying pull.", e);
                try
                {
                    PullRepo(localRepoPath);
                }
                catch (Exception exception)
                {
                    Logger.Error("Failed to pull docs repo.", exception);
                }
            }
        }

        public static void RegenerateHtmlFromMarkdown(this IMiscellaneousClient miscClient, FileInfo fileInfo)
        {
            string repoPath = Repository.Discover(fileInfo.FullName);
            PullRepo(repoPath);
            miscClient.TryConvertFromMarkdown(fileInfo);
        }

        public static void StartHtmlUpdate(this IMiscellaneousClient miscClient, IAppSettings appSettings)
        {
            var localRepoPath = appSettings.GetString("LocalDocsRepoLocation");
            var rootDocsDir = new DirectoryInfo(localRepoPath);
            var wikiDocsDir = new DirectoryInfo(Path.Combine(rootDocsDir.FullName, "wiki"));
            var allMarkDownFiles = wikiDocsDir.GetFiles("*.md", SearchOption.AllDirectories).ToList();
            allMarkDownFiles.ForEach(markdownFile =>
            {
                var htmlFile = new FileInfo(markdownFile.FullName.Replace(".md", ".html"));
                if (htmlFile.Exists && (htmlFile.LastWriteTimeUtc >= markdownFile.LastWriteTimeUtc))
                {
                    return;
                }
                Logger.Debug("Updating HTML for {0}".Fmt(htmlFile.FullName));
                //Thottle
                Thread.Sleep(1000);
                try
                {
                    miscClient.TryConvertFromMarkdown(markdownFile);
                }
                catch (Exception)
                {
                    try
                    {
                        Thread.Sleep(5000);
                        miscClient.TryConvertFromMarkdown(markdownFile);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to convert to HTML for file {0}.", e);
                        // noop, will try again next sync any way..
                    }
                }
            });
        }

        public static void TryConvertFromMarkdown(this IMiscellaneousClient miscClient, FileInfo fileInfo)
        {
            var htmlFilePath = fileInfo.FullName.Replace(".md", ".html");
            var htmlResponseTask =
                    miscClient.RenderRawMarkdown(File.ReadAllText(fileInfo.FullName));
            htmlResponseTask.ConfigureAwait(false);
            var htmlResponse = htmlResponseTask.Result;
            File.WriteAllText(htmlFilePath, htmlResponse);
        }

        public static void PullRepo(string localRepoPath)
        {
            using (var repo = new Repository(localRepoPath))
            {
                var options = new PullOptions { FetchOptions = new FetchOptions() };
                Remote remote = repo.Network.Remotes["origin"];
                repo.Network.Fetch(remote);
                repo.Network.Pull(
                    new Signature("ServiceStackDocsBot", "docsbot@servicestack.net",
                        new DateTimeOffset(DateTime.Now)), options);
            }
        }
    }
}
