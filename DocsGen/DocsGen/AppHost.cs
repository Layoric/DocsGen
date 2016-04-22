using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Funq;
using DocsGen.ServiceInterface;
using LibGit2Sharp;
using Octokit;
using Octokit.Internal;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using ServiceStack.Logging.EventLog;
using ServiceStack.Razor;
using ServiceStack.Text;
using Credentials = Octokit.Credentials;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace DocsGen
{
    public class AppHost : AppHostBase
    {
        private ILog logger;
        private bool migrationEnabled;

        /// <summary>
        /// Default constructor.
        /// Base constructor requires a name and assembly to locate web service classes. 
        /// </summary>
        public AppHost()
            : base("DocsGen", typeof(MyServices).Assembly)
        {
            var customSettings = new FileInfo(@"~/appsettings.txt".MapHostAbsolutePath());
            AppSettings = customSettings.Exists
                ? (IAppSettings)new TextFileSettings(customSettings.FullName)
                : new AppSettings();
#if DEBUG
            var localCustomSettings = new FileInfo(@"~/wwwroot_build/deploy/appsettings.txt".MapHostAbsolutePath());
            AppSettings = localCustomSettings.Exists ? (IAppSettings)new TextFileSettings(localCustomSettings.FullName)
                : new AppSettings();
#endif
        }

        /// <summary>
        /// Application specific configuration
        /// This method should initialize any IoC resources utilized by your web service classes.
        /// </summary>
        /// <param name="container"></param>
        public override void Configure(Container container)
        {
            //Config examples
            //this.Plugins.Add(new PostmanFeature());
            //this.Plugins.Add(new CorsFeature());

            SetConfig(new HostConfig
            {
                DebugMode = AppSettings.Get("DebugMode", false),
                AddRedirectParamsToQueryString = true
            });

            LogManager.LogFactory = new EventLogFactory("DocsGen.Logging", "Application");
            logger = LogManager.GetLogger(typeof(MyServices));
            JsConfig.PropertyConvention = PropertyConvention.Lenient;
            JsConfig.EmitCamelCaseNames = false;
            JsConfig.EmitLowercaseUnderscoreNames = true;

            var ghUserId = AppSettings.GetString("GitHubUsername");
            var ghToken = AppSettings.GetString("GitHubToken");

            container.Register<IMiscellaneousClient>(c => new MiscellaneousClient(
                new Connection(
                    new ProductHeaderValue("SS"),
                    new InMemoryCredentialStore(new Credentials(ghUserId, ghToken)))
                ));
            container.Register<IAppSettings>(new AppSettings());

            migrationEnabled = AppSettings.Get<bool>("MigrationEnabled");

            InitLocalPaths();
            UpdateLocalRepo();
            if(migrationEnabled)
                MigrateExistingWiki();
            StartHtmlUpdate(container);
        }

        private void InitLocalPaths()
        {
            var localWikiPath = AppSettings.GetString("LocalWikiLocation");
            var localRepoPath = AppSettings.GetString("LocalRepoLocation");
            var wikiDirInfo = new DirectoryInfo(localWikiPath);
            var localRepoDirInfo = new DirectoryInfo(localRepoPath);
            if (!wikiDirInfo.Exists)
                wikiDirInfo.Create();
            if (!localRepoDirInfo.Exists)
                localRepoDirInfo.Create();
        }

        private void UpdateLocalRepo()
        {
            var ownerName = AppSettings.GetString("DocsRepoOwner");
            var repoName = AppSettings.GetString("DocsRepoName");
            var networkRepoUrl = "https://github.com/" + ownerName + "/" + repoName + ".git";
            var localRepoPath = AppSettings.GetString("LocalRepoLocation");
            try
            {
                Repository.Clone(networkRepoUrl, localRepoPath, new CloneOptions());
            }
            catch (Exception e)
            {
                logger.Error("Failed to clone docs. Trying pull.", e);
                using (var repo = new Repository(localRepoPath))
                {
                    var options = new PullOptions {FetchOptions = new FetchOptions()};
                    repo.Network.Pull(
                        new Signature("ServiceStackDocsBot", "docsbot@servicestack.net",
                            new DateTimeOffset(DateTime.Now)), options);
                }
            }
        }

        private void CreateDocsWikiDirectoryIfNotExists()
        {
            var localRepoPath = AppSettings.GetString("LocalRepoLocation");
            var localRepoDirInfo = new DirectoryInfo(localRepoPath);
            var localRepoWikiDirInfo = new DirectoryInfo(Path.Combine(localRepoDirInfo.FullName, "wiki"));
            if (!localRepoWikiDirInfo.Exists)
                localRepoWikiDirInfo.Create();
        }

        private void MigrateExistingWiki()
        {
            var ownerName = AppSettings.GetString("WikiRepoOwner");
            var repoName = AppSettings.GetString("WikiRepoName");
            var networkRepoUrl = "https://github.com/" + ownerName + "/" + repoName + ".git";
            var localWikiPath = AppSettings.GetString("LocalWikiLocation");
            var localRepoPath = AppSettings.GetString("LocalRepoLocation");

            try
            {
                Repository.Clone(networkRepoUrl, localWikiPath, new CloneOptions {  });
                // Has to create after clone due to git complaining trying to clone into new directory.
                CreateDocsWikiDirectoryIfNotExists();
            }
            catch (Exception e)
            {
                logger.Error("Failed to clone wiki. Trying pull.", e);
                using (var repo = new Repository(localWikiPath))
                {
                    var options = new PullOptions { FetchOptions = new FetchOptions() };
                    repo.Network.Pull(
                        new Signature("ServiceStackDocsBot", "docsbot@servicestack.net",
                            new DateTimeOffset(DateTime.Now)), options);
                }
            }

            var cleanMigrationOnStart = AppSettings.Get<bool>("CleanMigrationOnStart");

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
                logger.Debug("Copying file from {0} \n To: \n {1}".Fmt(wikiFile.FullName, destPath));
                File.Copy(wikiFile.FullName,destPath,true);
            });

            WikiDocumentMappings.MarkdownMappings.ForEach((key, val) =>
            {
                var sourcePath = Path.Combine(localRepoDirInfo.FullName, key.Replace("/", "\\"));
                var destPath = Path.Combine(localRepoDirInfo.FullName, val.Replace("/", "\\"));
                File.Copy(sourcePath,destPath,true);
            });
        }

        private void StartHtmlUpdate(Container container)
        {

            Task.Run(() =>
            {
                var localRepoPath = AppSettings.GetString("LocalRepoLocation");
                var rootDocsDir = new DirectoryInfo(localRepoPath);
                var wikiDocsDir = new DirectoryInfo(Path.Combine(rootDocsDir.FullName,"wiki"));
                var allMarkDownFiles = wikiDocsDir.GetFiles("*.md", SearchOption.AllDirectories).ToList();
                var miscClient = container.Resolve<IMiscellaneousClient>();
                allMarkDownFiles.ForEach(fileInfo =>
                {
                    //Thottle
                    Thread.Sleep(1000);
                    try
                    {
                        miscClient.TryConvertFromMarkdown(fileInfo);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            Thread.Sleep(5000);
                            miscClient.TryConvertFromMarkdown(fileInfo);
                        }
                        catch (Exception e)
                        {
                            logger.Error("Failed to convert to HTML for file {0}.", e);
                            // noop, will try again next sync any way..
                        }
                    }
                });
            });
        }
    }
}