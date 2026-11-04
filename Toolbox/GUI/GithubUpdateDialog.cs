using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library.Forms;
using Octokit;

namespace Toolbox
{
    public partial class GithubUpdateDialog : STForm
    {
        public GithubUpdateDialog()
        {
            InitializeComponent();
        }

        private List<GitHubCommit> ActiveCommitList;
        public void LoadCommits(List<GitHubCommit> Commits)
        {
            ActiveCommitList = Commits;
            listViewCustom1.Items.Clear();
            foreach (var commit in Commits)
            {
                var item = listViewCustom1.Items.Add(FirstLine(commit.Commit.Message));
                item.SubItems.Add(commit.Commit.Author.Date.LocalDateTime.ToString("g"));
            }
            if (Commits.Count > 0)
            {
                stTextBox1.Text = Commits[0].Commit.Message;
                listViewCustom1.Items[0].Selected = true;
            }
        }

        public void LoadReleaseNotes(Release release)
        {
            ActiveCommitList = null;
            listViewCustom1.Items.Clear();
            var item = listViewCustom1.Items.Add(release.Name ?? release.TagName ?? "New release");
            item.SubItems.Add(release.PublishedAt.HasValue ? release.PublishedAt.Value.LocalDateTime.ToString("g") : "Date unavailable");
            item.Selected = true;
            stTextBox1.Text = string.IsNullOrWhiteSpace(release.Body) ? "No release notes were provided." : release.Body;
            Text = $"Switch Toolbox 2 update: {release.TagName ?? release.Name}";
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(no message)";
            using (var reader = new System.IO.StringReader(text)) return reader.ReadLine();
        }

        private void listViewCustom1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewCustom1.SelectedIndices.Count > 0)
            {
                int index = listViewCustom1.SelectedIndices[0];
                if (ActiveCommitList != null && index < ActiveCommitList.Count)
                    stTextBox1.Text = ActiveCommitList[index].Commit.Message;
            }
        }
    }
}
