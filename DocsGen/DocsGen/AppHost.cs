using System.IO;
using Funq;
using DocsGen.ServiceInterface;
using DocsGen.ServiceInterface.Helpers;
using Octokit;
using Octokit.Internal;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Logging;
using ServiceStack.Logging.EventLog;
using ServiceStack.Text;
using Credentials = Octokit.Credentials;

namespace DocsGen
{
    public class AppHost : AppHostBase
    {
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
            var miscClient = container.Resolve<IMiscellaneousClient>();

            migrationEnabled = AppSettings.Get<bool>("MigrationEnabled");

            InitLocalPaths();
            GitHelpers.UpdateLocalRepo(AppSettings);

            if(migrationEnabled)
                Migration.MigrateExistingWiki(AppSettings);

            miscClient.StartHtmlUpdate(AppSettings);
            GitHelpers.CommitChangesToDocs(AppSettings);
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
    }
}