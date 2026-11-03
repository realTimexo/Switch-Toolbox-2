using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Linq;
using Toolbox.Library;
using System.Reflection;

namespace Toolbox
{
    public class UpdateProgram
    {
        static List<Release> Releases = new List<Release>();
        public static bool CanUpdate = false;
        public static bool Downloaded = false;
        public static Release LatestRelease;
        public static List<GitHubCommit> CommitList = new List<GitHubCommit>();
        public static DateTime LatestReleaseTime;


        public static void CheckLatest()
        {
            try
            {
                VersionCheck versionCheck = new VersionCheck(true);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var client = new GitHubClient(new ProductHeaderValue("ST_UpdateTool"));

                GetReleases(client).Wait();

                var info = client.GetLastApiInfo();
                if (info != null && info.RateLimit.Remaining <= 0)
                    return;

                GetCommits(client).Wait();

                Release latestRelease = Releases.FirstOrDefault();
                ReleaseAsset latestAsset = GetReleaseAsset(latestRelease);
                if (latestRelease == null || latestAsset == null)
                    return;

                if (NormalizeVersion(versionCheck.ProgramVersion) == NormalizeVersion(latestRelease.TagName))
                {
                    Runtime.ProgramVersion = latestRelease.TagName;
                    Runtime.CompileDate = latestAsset.UpdatedAt.ToString();
                    Runtime.CommitInfo = latestRelease.TargetCommitish;
                    return;
                }

                LatestReleaseTime = latestAsset.UpdatedAt.DateTime;
                LatestRelease = latestRelease;
                CanUpdate = true;
            }
            catch (Exception ex)
            {
            }
        }

        static void DownloadRelease()
        {
            ProcessStartInfo p = new ProcessStartInfo();
            p.WindowStyle = ProcessWindowStyle.Hidden;
            p.CreateNoWindow = true;
            p.FileName = Path.Combine(Runtime.ExecutableDir, "Updater.exe");
            p.WorkingDirectory = Path.Combine(Runtime.ExecutableDir, "updater/");
            Console.WriteLine($"Updater: {p.FileName}");
            p.Arguments = "-d";

            Process process = new Process();
            process.StartInfo = p;
            Console.WriteLine("Downloading...");
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new TimeoutException();
            Console.WriteLine("Finished downloading");
            string updateExe = Path.Combine(Runtime.ExecutableDir, "master\\Toolbox.exe"),
                  currentExe = System.Reflection.Assembly.GetEntryAssembly().Location;
            if (!Utils.CreateMD5Hash(currentExe).SequenceEqual(Utils.CreateMD5Hash(updateExe)))
                CanUpdate = true;
        }

        static async Task GetCommits(GitHubClient client)
        {
            var options = new ApiOptions
            {
                PageSize = 20,
                PageCount = 1
            };

            DateTimeOffset CurrentRelease;
            bool IsValidTime = DateTimeOffset.TryParse(Runtime.CompileDate, out CurrentRelease);

            foreach (GitHubCommit c in await client.Repository.Commit.GetAll("realTimexo", "Switch-Toolbox-2", options))
            {
                if (IsValidTime)
                {
                    if (CurrentRelease.DateTime < c.Commit.Author.Date.DateTime)
                        CommitList.Add(c);
                    else
                        break;
                }
                else
                {
                    //Just add extra commits. This shouldn't happen unless the user actually edits the file
                    CommitList.Add(c);
                }
            }
        }

        static async Task GetReleases(GitHubClient client)
        {
            Releases = new List<Release>();
            foreach (Release r in await client.Repository.Release.GetAll("realTimexo", "Switch-Toolbox-2"))
            {
                if (!r.Draft && !r.Prerelease && GetReleaseAsset(r) != null)
                    Releases.Add(r);
            }
        }

        private static ReleaseAsset GetReleaseAsset(Release release)
        {
            return release?.Assets?.FirstOrDefault(asset =>
                asset.Name.StartsWith("SwitchToolbox2-", StringComparison.OrdinalIgnoreCase) &&
                asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim().TrimStart('v', 'V');
        }
    }
}
