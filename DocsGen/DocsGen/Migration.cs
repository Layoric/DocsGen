using System;
using System.IO;
using System.Linq;
using DocsGen.ServiceInterface;
using LibGit2Sharp;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;

namespace DocsGen
{
    public static class Migration
    {
        private static ILog Logger => LogManager.GetLogger(typeof(MyServices));

        public static void CreateDocsWikiDirectoryIfNotExists(IAppSettings appSettings)
        {
            var localRepoPath = appSettings.GetString("LocalRepoLocation");
            var localRepoDirInfo = new DirectoryInfo(localRepoPath);
            var localRepoWikiDirInfo = new DirectoryInfo(Path.Combine(localRepoDirInfo.FullName, "wiki"));
            if (!localRepoWikiDirInfo.Exists)
                localRepoWikiDirInfo.Create();
        }

        public static void MigrateExistingWiki(IAppSettings appSettings)
        {
            var ownerName = appSettings.GetString("WikiRepoOwner");
            var repoName = appSettings.GetString("WikiRepoName");
            var networkRepoUrl = "https://github.com/" + ownerName + "/" + repoName + ".git";
            var localWikiPath = appSettings.GetString("LocalWikiLocation");
            var localRepoPath = appSettings.GetString("LocalRepoLocation");

            try
            {
                Repository.Clone(networkRepoUrl, localWikiPath, new CloneOptions());
                // Has to create after clone due to git complaining trying to clone into new directory.
                CreateDocsWikiDirectoryIfNotExists(appSettings);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to clone wiki. Trying pull.", e);
                using (var repo = new Repository(localWikiPath))
                {
                    var options = new PullOptions { FetchOptions = new FetchOptions() };
                    repo.Network.Pull(
                        new Signature("ServiceStackDocsBot", "docsbot@servicestack.net",
                            new DateTimeOffset(DateTime.Now)), options);
                }
            }

            var cleanMigrationOnStart = appSettings.Get<bool>("CleanMigrationOnStart");

            var localRepoDirInfo = new DirectoryInfo(localRepoPath);
            if (cleanMigrationOnStart)
            {
                // Wipe existing files, this is to handle renames during mirgration to do repo.
                var relativeLocalDocsWikiDirectory = Path.Combine(localRepoDirInfo.FullName, "wiki");
                var allFiles = new DirectoryInfo(relativeLocalDocsWikiDirectory).GetFiles().ToList();
                allFiles.ForEach(x => x.Delete());
            }

            var wikiDirInfo = new DirectoryInfo(localWikiPath);

            var wikiFiles = wikiDirInfo.GetFiles("*.md", SearchOption.AllDirectories).ToList();

            wikiFiles.ForEach(wikiFile =>
            {
                string relativePath = wikiFile.FullName.Replace(wikiDirInfo.FullName + "\\", "");
                string destPath = Path.Combine(localRepoDirInfo.FullName + "\\wiki", relativePath);
                Logger.Debug("Copying file from {0} \n To: \n {1}".Fmt(wikiFile.FullName, destPath));
                File.Copy(wikiFile.FullName, destPath, true);
            });

            WikiDocumentMappings.MarkdownMappings.ForEach((key, val) =>
            {
                var sourcePath = Path.Combine(localRepoDirInfo.FullName, key.Replace("/", "\\"));
                var destPath = Path.Combine(localRepoDirInfo.FullName, val.Replace("/", "\\"));
                File.Copy(sourcePath, destPath, true);
            });
        }
    }
}
