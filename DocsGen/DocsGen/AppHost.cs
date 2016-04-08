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
using ServiceStack.Razor;
using ServiceStack.Text;
using Credentials = Octokit.Credentials;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace DocsGen
{
    public class AppHost : AppHostBase
    {
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

            LogManager.LogFactory = new ConsoleLogFactory();
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

            InitAndUpdateLocalRepo();
            StartHtmlUpdate(container);
        }

        private void InitAndUpdateLocalRepo()
        {
            var ownerName = AppSettings.GetString("DocsRepoOwner");
            var repoName = AppSettings.GetString("DocsRepoName");
            var networkRepoUrl = "https://github.com/" + ownerName + "/" + repoName + ".git";
            var localRepoPath = AppSettings.GetString("LocalRepoLocation");
            try
            {
                Repository.Clone(networkRepoUrl, "c:\\src\\ServiceStack\\docs", new CloneOptions());
            }
            catch (Exception)
            {
                using (var repo = new Repository(localRepoPath))
                {
                    var options = new PullOptions {FetchOptions = new FetchOptions()};
                    repo.Network.Pull(
                        new Signature("ServiceStackDocsBot", "docsbot@servicestack.net",
                            new DateTimeOffset(DateTime.Now)), options);
                }
            }
        }

        private void StartHtmlUpdate(Container container)
        {
            
            Task.Run(() =>
            {
                var localRepoPath = AppSettings.GetString("LocalRepoLocation");
                DirectoryInfo rootDocsDir = new DirectoryInfo(localRepoPath);
                var allMarkDownFiles = rootDocsDir.GetFiles("*.md", SearchOption.AllDirectories).ToList();
                var miscClient = container.Resolve<IMiscellaneousClient>();
                allMarkDownFiles.ForEach(fileInfo =>
                {
                    //Thottle
                    Thread.Sleep(500);
                    var htmlFilePath = fileInfo.FullName.Replace(".md", ".html");
                    var htmlResponseTask =
                            miscClient.RenderRawMarkdown(File.ReadAllText(fileInfo.FullName));
                    htmlResponseTask.ConfigureAwait(false);
                    var htmlResponse = htmlResponseTask.Result;
                    File.WriteAllText(htmlFilePath, htmlResponse);
                });
            });
        }
    }
}