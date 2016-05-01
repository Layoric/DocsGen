using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace DocsGen.ServiceModel
{
    [Route("/webhooks/wiki")]
    public class GitHubGollumEvent : IReturnVoid
    {
        public GitHubGollumEvent()
        {
            Pages = new List<GitHubPage>();
        }

        public string Ref { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public bool Created { get; set; }
        public bool Deleted { get; set; }
        public bool Forced { get; set; }
        public object BaseRef { get; set; }
        public string Compare { get; set; }

        public List<GitHubPage> Pages { get; set; }
        public GitHubRepository Repository { get; set; }
    }

    [Route("/webhook/docs")]
    public class GitHubCommitEvent : IReturnVoid
    {
        public GitHubCommitEvent()
        {
            Commits = new List<GitCommit>();
        }

        public string Ref { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public bool Created { get; set; }
        public bool Deleted { get; set; }
        public bool Forced { get; set; }
        public object BaseRef { get; set; }
        public string Compare { get; set; }

        public List<GitCommit> Commits { get; set; }
        public GitHubRepository Repository { get; set; }
        public GitHubCommiter Committer { get; set; }
    }

    public class GitHubPusher
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class GitHubCommiter
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// Only exists for Push event
    /// </summary>
    public class GitCommit
    {
        public string Id { get; set; }
        public bool Distinct { get; set; }
        public string Message { get; set; }
        public string Timestamp { get; set; }
        public string Url { get; set; }
        public Committer Committer { get; set; }
        public List<string> Added { get; set; }
        public List<string> Removed { get; set; }
        public List<string> Modified { get; set; }
    }

    public class GitHubPage
    {
        public string PageName { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Action { get; set; }
        public string Sha { get; set; }
        public string HtmlUrl { get; set; }
    }

    public class Committer
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
    }

    public class GitHubRepository
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Name { get; set; }
        public GitHubRepositoryOwner Owner { get; set; }
    }

    public class GitHubRepositoryOwner
    {
        public string Login { get; set; }
    }
}
