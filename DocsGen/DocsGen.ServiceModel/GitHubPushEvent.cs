using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace DocsGen.ServiceModel
{
    [Route("/docspush")]
    public class GitHubPushEvent : IReturnVoid
    {
        public string Ref { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public bool Created { get; set; }
        public bool Deleted { get; set; }
        public bool Forced { get; set; }
        public object BaseRef { get; set; }
        public string Compare { get; set; }
        public HeadCommit HeadCommit { get; set; }
        public GitHubRepository Repository { get; set; }
    }

    public class HeadCommit
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
    }
}
