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

                listViewCustom1.Items.Add(commit.Commit.Message).SubItems.Add(commit.Commit.Author.Date.LocalDateTime.ToString());
            }
        }

        public void LoadReleaseNotes(Release release)
        {
            ActiveCommitList = null;
            listViewCustom1.Items.Clear();
            stTextBox1.Text = release.Body ?? string.Empty;
            Text = $"Switch Toolbox 2 update: {release.TagName}";
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
