using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using VGAudio.Containers.Wave;
using VGAudio.Formats;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinformsVisualization.Visualization;
using CSCore.DSP;
using CSCore.Streams.Effects;
using CSCore;
using CSCore.SoundOut;
using CSCore.CoreAudioAPI;
using CSCore.Tags.ID3;

namespace Toolbox.Library.Forms
{
    public partial class AudioPlayerPanel : STUserControl
    {
        private LineSpectrum lineSpectrum;
        private PitchShifter _pitchShifter;
        private bool stopSliderUpdate;
        private MMDevice activeDevice;

        public AudioFile selectedFile;

        public VisualSetting visualSetting = VisualSetting.WaveSpectum;

        public List<IFileFormat> AudioFileFormats = new List<IFileFormat>();

        public enum VisualSetting
        {
            Artwork,
            WaveSpectum,
        }

        private AudioChannel GetActiveAudio()
        {
            if (selectedFile == null)
                return null;

            return selectedFile.GetCurrentChannel();
        }

        public AudioPlayerPanel()
        {
            InitializeComponent();
            SetTheme();
            SetDevices();
            audioListView.FillLastColumnSpace(true);
            var audioContextMenu = new ContextMenuStrip();
            var importWaveItem = new ToolStripMenuItem("Import WAV...");
            var exportBinaryWaveItem = new ToolStripMenuItem("Export BWAV...");
            var settingsItem = new ToolStripMenuItem("BWAV export settings...");
            importWaveItem.Click += ImportWaveToBinaryWave;
            exportBinaryWaveItem.Click += ExportBinaryWave;
            settingsItem.Click += (sender, args) =>
            {
                var target = selectedFile == null ? null : selectedFile.vgAdudioFile;
                var settings = target == null ? null : target.Format as IBinaryWaveSettings;
                if (settings != null) settings.EditSettings(this);
            };
            audioContextMenu.Items.Add(importWaveItem);
            audioContextMenu.Items.Add(settingsItem);
            audioContextMenu.Items.Add(exportBinaryWaveItem);
            audioContextMenu.Opening += (sender, args) =>
            {
                var source = selectedFile == null ? null : selectedFile.vgAdudioFile;
                bool isBinaryWave = source != null && source.Format != null &&
                    source.Format.Extension.Any(ext => ext.Equals("*.bwav", StringComparison.OrdinalIgnoreCase));
                importWaveItem.Enabled = isBinaryWave;
                exportBinaryWaveItem.Enabled = isBinaryWave;
                settingsItem.Enabled = isBinaryWave && source.Format is IBinaryWaveSettings;
            };
            audioListView.ContextMenuStrip = audioContextMenu;
        }

        private void ExportBinaryWave(object sender, EventArgs e)
        {
            var target = selectedFile == null ? null : selectedFile.vgAdudioFile;
            if (target == null || target.audioData == null || target.Format == null ||
                !target.Format.Extension.Any(ext => ext.Equals("*.bwav", StringComparison.OrdinalIgnoreCase))) return;

            using (var dialog = new SaveFileDialog
            {
                Filter = "Nintendo AAL Binary Wave (*.bwav)|*.bwav",
                DefaultExt = "bwav",
                AddExtension = true,
                FileName = Path.GetFileNameWithoutExtension(selectedFile.Title) + ".bwav"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var output = File.Create(dialog.FileName))
                        ((IFileFormat)target).Save(output);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The BWAV file could not be exported: " + ex.Message,
                        "Switch Toolbox 2", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportWaveToBinaryWave(object sender, EventArgs e)
        {
            var target = selectedFile == null ? null : selectedFile.vgAdudioFile;
            if (target == null || target.Format == null ||
                !target.Format.Extension.Any(ext => ext.Equals("*.bwav", StringComparison.OrdinalIgnoreCase)))
                return;

            using (var dialog = new OpenFileDialog { Filter = "Wave audio (*.wav)|*.wav", CheckFileExists = true })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var input = File.OpenRead(dialog.FileName))
                        target.audioData = new WaveReader().ReadWithConfig(input).Audio;
                    LoadFile(target.audioData, (IFileFormat)target);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The WAV file could not be imported: " + ex.Message,
                        "Switch Toolbox 2", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #region File Open

        public void LoadFile(string AudioFileName)
        {

        }

        public void LoadFile(byte[] AudioData)
        {
            
        }

        public void LoadFile(IWaveSource source, IFileFormat fileFormat, bool ClearPlaylist = true, object AudioStruct = null)
        {
            if (ClearPlaylist)
                ResetAudioList();

            AudioFile file = new AudioFile();
            file.Title = fileFormat.FileName;

            if (AudioStruct is ID3v1)
            {
                var mp3 = (ID3v1)AudioStruct;

                file.Title = mp3.Title;
                file.Artist = mp3.Artist;
            }

            AudioFileFormats.Add(fileFormat);

            AudioChannel audioChannel = new AudioChannel();
            audioChannel.Name = $"Channel [0]";
            file.Channels.Add(audioChannel);

            audioChannel.audioPlayer.Open(source, activeDevice);

            audioChannel.audioPlayer.PlaybackStopped += (s, args) =>
            {
                //WasapiOut uses SynchronizationContext.Post to raise the event
                //There might be already a new WasapiOut-instance in the background when the async Post method brings the PlaybackStopped-Event to us.
                if (audioChannel.audioPlayer.PlaybackState != PlaybackState.Stopped)
                {

                }
            };

            audioListView.AddObject(file);
            audioListView.UpdateObject(file);

        }

        public void LoadFile(AudioData audioData, IFileFormat fileFormat, bool ClearPlaylist = true)
        {
            if (ClearPlaylist)
                ResetAudioList();

            AudioFileFormats.Add(fileFormat);

            //Load Channel Info
            AudioFile file = new AudioFile();
            file.Title = fileFormat.FileName;
            if (fileFormat is VGAdudioFile)
                file.vgAdudioFile = (VGAdudioFile)fileFormat;

            //The first entry is the full interleaved signal; subsequent entries let the user audition each speaker channel.
            var format = audioData.GetAllFormats().ToArray()[0];
            AddPlaybackChannel(file, "All channels", format.ToPcm16());
            for (int c = 0; c < format.ChannelCount; c++)
            {
                string name = $"Channel [{c}]";
                var bwavSettings = fileFormat as IBinaryWaveSettings;
                if (bwavSettings != null && bwavSettings.TargetChannelPans != null && c < bwavSettings.TargetChannelPans.Length)
                    name = bwavSettings.TargetChannelPans[c] == 0 ? $"Channel [{c}] - Left" :
                        bwavSettings.TargetChannelPans[c] == 1 ? $"Channel [{c}] - Right" : $"Channel [{c}] - Center";
                AddPlaybackChannel(file, name, format.GetChannels(c).ToPcm16());
            }

            audioListView.AddObject(file);

            if (audioListView.Items.Count != 0)
                audioListView.SelectedIndex = 0;
        }

        private void AddPlaybackChannel(AudioFile file, string name, VGAudio.Formats.Pcm16.Pcm16Format audio)
        {
            using (var wave = new MemoryStream())
            {
                new WaveWriter().WriteToStream(audio, wave);
                var channel = new AudioChannel { Name = name, Data = wave.ToArray() };
                file.Channels.Add(channel);
                channel.audioPlayer.Open(new MemoryStream(channel.Data), "playback.wav", activeDevice);
                channel.audioPlayer.PlaybackStopped += (s, args) =>
                {
                    if (channel.audioPlayer.PlaybackState != PlaybackState.Stopped) { }
                };
            }
        }

        #endregion


        #region GUI

        private void UpdateAudioData()
        {
            foreach (var audio in audioListView.Objects)
                audioListView.RefreshObject(audio);
        }

        private void FillArtPanel()
        {
            switch (visualSetting)
            {
                case VisualSetting.Artwork:
                    break;
                case VisualSetting.WaveSpectum:
                    GenerateLineSpectrum();
                    break;
            }
        }

        private void FillForm()
        {
            if (selectedFile == null)
            {
                editToolStripMenuItem.Enabled = false;
                return;
            }
            editToolStripMenuItem.Enabled = true;

            //Load the full mix and per-channel audition streams.
            channelCB.Items.Clear();
            for (int i = 0; i < selectedFile.Channels.Count; i++)
            {
                channelCB.Items.Add(selectedFile.Channels[i].Name);
            }
            channelCB.SelectedIndex = selectedFile.SelectedChannelIndex;

            //Setup a sample source for spectum data
            lineSpectrum = (selectedFile.GetCurrentChannel().audioPlayer._lineSpectrum);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var channel = GetActiveAudio();
            if (channel == null)
                return;

            TimeSpan position = channel.audioPlayer.Position;
            TimeSpan length = channel.audioPlayer.Length;
            if (position > length)
            {
                length = position;
            }
            if (position >= length)
            {
                if (chkLoopPlayer.Checked)
                {
                    channel.audioPlayer.Position = TimeSpan.Zero;
                    colorSlider1.Value = 0;
                    channel.audioPlayer.Play();
                }
            }


            stLabel1.Text = String.Format(@"{0:mm\:ss}", position);
            stLabel2.Text = String.Format(@"{0:mm\:ss}", length);

            FillArtPanel();

            if (!stopSliderUpdate &&
                length != TimeSpan.Zero && position != TimeSpan.Zero)
            {
                double perc = position.TotalMilliseconds / length.TotalMilliseconds * colorSlider1.Maximum;
                colorSlider1.Value = (int)perc;
            }
            audioListView.RefreshObject(selectedFile);
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            var channel = GetActiveAudio();
            if (channel == null)
                return;


            UpdateAudioData();

            //Pause if being played, or vice versa
            if (channel.audioPlayer.PlaybackState == PlaybackState.Playing)
            {
                channel.audioPlayer.Pause();
                btnPlay.BackgroundImage = Properties.Resources.PlayArrowR;
            }
            else
            {
                channel.audioPlayer.Play();
                btnPlay.BackgroundImage = Properties.Resources.PauseBtn;
            }

            //Set the status in the list and update it
            selectedFile.Status = channel.audioPlayer.PlaybackState.ToString();
        }

        private void audioListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (audioListView.SelectedObject != null)
            {
                if ((AudioFile)audioListView.SelectedObject == selectedFile)
                    return;

                selectedFile = (AudioFile)audioListView.SelectedObject;
                stLabel2.Text = selectedFile.Duration;

                FillForm();
            }
        }

        #endregion

        #region SpectumBar

     
        private void GenerateLineSpectrum()
        {
            if (lineSpectrum == null)
                return;

            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            Image image = pictureBox1.Image;
            var newImage = lineSpectrum.CreateSpectrumLine(pictureBox1.Size, Color.Green, Color.Red, Color.Black, true);
            if (newImage != null)
            {
                pictureBox1.Image = newImage;
                if (image != null)
                    image.Dispose();
            }
        }

        #endregion

        #region Themes
        private void SetTheme()
        {
            foreach (BrightIdeasSoftware.OLVColumn column in audioListView.Columns)
            {
                var headerstyle = new BrightIdeasSoftware.HeaderFormatStyle();
                headerstyle.SetBackColor(FormThemes.BaseTheme.FormBackColor);
                headerstyle.SetForeColor(FormThemes.BaseTheme.FormForeColor);
                column.HeaderFormatStyle = headerstyle;
            }

            stPanel1.BackColor = FormThemes.BaseTheme.ObjectEditorBackColor;

            btnPlay.BackColor = stPanel1.BackColor;
            btnBackward1.BackColor = stPanel1.BackColor;
            btnForward1.BackColor = stPanel1.BackColor;
            audioListView.BackColor = FormThemes.BaseTheme.TextEditorBackColor;
            audioListView.FullRowSelect = true;
            audioBarPanel.BackColor = stPanel1.BackColor;
        }
        #endregion

        #region Audio Devices

        //Get audio devices to play audio out
        private void SetDevices()
        {
            using (var mmdeviceEnumerator = new MMDeviceEnumerator())
            {
                using (
                    var mmdeviceCollection = mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
                {
                    foreach (var device in mmdeviceCollection)
                    {
                        audioDevice.Items.Add(device);
                    }
                }
            }
            if (audioDevice.Items.Count <= 0)
                throw new Exception("No audio devices found!");

            audioDevice.SelectedIndex = 0;

            activeDevice = (MMDevice)audioDevice.SelectedItem;
        }

        #endregion

        private void ResetPlayers()
        {
            if (selectedFile == null || selectedFile.Channels == null)
                return;

            foreach (var channel in selectedFile.Channels)
            {
                channel.audioPlayer.Stop();
                channel.audioPlayer.Position = TimeSpan.Zero;
            }

            colorSlider1.Value = 0;
            btnPlay.BackgroundImage = Properties.Resources.PlayArrowR;
        }

        private void colorSlider1_ValueChanged(object sender, EventArgs e)
        {
            var channel = GetActiveAudio();
            if (channel == null)
                return;

            if (stopSliderUpdate)
            {
                double perc = colorSlider1.Value / (double)colorSlider1.Maximum;
                TimeSpan position = TimeSpan.FromMilliseconds(channel.audioPlayer.Length.TotalMilliseconds * perc);
                channel.audioPlayer.Position = position;
            }
        }

        private void colorSlider1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                stopSliderUpdate = true;
        }

        private void colorSlider1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                stopSliderUpdate = false;
        }

        private void trackbarVolume_ValueChanged(object sender, EventArgs e)
        {
            var channel = GetActiveAudio();
            if (channel == null)
                return;

            channel.audioPlayer.Volume = trackbarVolume.Value;
        }

        public void ResetAudioList()
        {
            ResetPlayers();

            channelCB.Items.Clear();
            if (audioListView.Objects == null)
                return;

            foreach (var obj in audioListView.Objects)
            {
                foreach (var chan in ((AudioFile)obj).Channels)
                {
                    chan.audioStream.Dispose();
                    chan.audioPlayer.Dispose();
                    chan.samplerSource.Dispose();
                }

                ((AudioFile)obj).Dispose();
            }
            audioListView.ClearObjects();
        }

        public override void OnControlClosing()
        {
            Console.WriteLine("Closing audio panel");

            var channel = GetActiveAudio();
            if (channel != null)
            {
                channel.audioPlayer.Stop();
                channel.audioPlayer.Position = TimeSpan.Zero;
                colorSlider1.Value = 0;
            }

            ResetAudioList();
        }

        private void loopingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedFile != null && selectedFile.vgAdudioFile != null)
            {
                LoopEditor editor = new LoopEditor();
                var audioFile = selectedFile.vgAdudioFile;
                var bwav = audioFile.Format as IBinaryWaveSettings;
                int sampleCount = 0;
                if (audioFile.audioData != null)
                {
                    var audioFormat = audioFile.audioData.GetAllFormats().FirstOrDefault();
                    if (audioFormat != null) sampleCount = audioFormat.SampleCount;
                }
                editor.startLoopUD.Maximum = Math.Max(1, sampleCount);
                editor.endLoopUD.Maximum = Math.Max(1, sampleCount);

                if (bwav != null)
                {
                    editor.chkCanLoop.Checked = bwav.TargetLooping;
                    editor.startLoopUD.Value = Math.Min(editor.startLoopUD.Maximum, bwav.TargetLoopStartSample);
                    uint end = bwav.TargetLoopEndSample == uint.MaxValue || bwav.TargetLoopEndSample == 0
                        ? (uint)sampleCount : bwav.TargetLoopEndSample;
                    editor.endLoopUD.Value = Math.Min(editor.endLoopUD.Maximum, end);
                }
                else if (audioFile.audioWithConfig != null && audioFile.audioWithConfig.AudioFormat != null)
                {
                    editor.chkCanLoop.Checked = audioFile.audioWithConfig.AudioFormat.Looping;
                    editor.startLoopUD.Value = Math.Min(editor.startLoopUD.Maximum, audioFile.audioWithConfig.AudioFormat.LoopStart);
                    editor.endLoopUD.Value = Math.Min(editor.endLoopUD.Maximum, audioFile.audioWithConfig.AudioFormat.LoopEnd);
                }
                else return;

                if (editor.ShowDialog() == DialogResult.OK)
                {
                    bool enabled = editor.chkCanLoop.Checked;
                    int start = (int)editor.startLoopUD.Value;
                    int end = (int)editor.endLoopUD.Value;
                    if (bwav != null)
                    {
                        bwav.TargetLooping = enabled;
                        bwav.TargetLoopStartSample = (uint)start;
                        bwav.TargetLoopEndSample = (uint)end;
                    }
                    if (audioFile.audioData != null)
                    {
                        if (enabled) audioFile.audioData.SetLoop(true, start, end);
                        else audioFile.audioData.SetLoop(false);
                    }
                }
            }

     
        }

        private void audioDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (audioDevice.SelectedItem == null || !(audioDevice.SelectedItem is MMDevice))
                return;
            activeDevice = (MMDevice)audioDevice.SelectedItem;
            if (selectedFile == null || selectedFile.Channels == null)
                return;

            foreach (var channel in selectedFile.Channels)
            {
                if (channel.Data == null) continue;
                var player = channel.audioPlayer;
                PlaybackState state = player.PlaybackState;
                TimeSpan position = player.Position;
                int volume = player.Volume;
                player.Open(new MemoryStream(channel.Data), "playback.wav", activeDevice);
                player.Position = position;
                player.Volume = volume;
                if (state == PlaybackState.Playing) player.Play();
                else if (state == PlaybackState.Paused) { player.Play(); player.Pause(); }
            }
        }

        private void channelCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (selectedFile == null || channelCB.SelectedIndex < 0 ||
                channelCB.SelectedIndex >= selectedFile.Channels.Count ||
                channelCB.SelectedIndex == selectedFile.SelectedChannelIndex)
                return;

            var previous = selectedFile.GetCurrentChannel();
            PlaybackState previousState = previous.audioPlayer.PlaybackState;
            TimeSpan previousPosition = previous.audioPlayer.Position;
            int previousVolume = previous.audioPlayer.Volume;
            ResetPlayers();
            selectedFile.SelectedChannelIndex = channelCB.SelectedIndex;
            var current = selectedFile.GetCurrentChannel();
            current.audioPlayer.Position = previousPosition;
            current.audioPlayer.Volume = previousVolume;
            if (previousState == PlaybackState.Playing) current.audioPlayer.Play();
            else if (previousState == PlaybackState.Paused) { current.audioPlayer.Play(); current.audioPlayer.Pause(); }
            trackbarVolume.Value = current.audioPlayer.Volume;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            var channel = GetActiveAudio();
            if (channel == null)
                return;

            if (channel.audioPlayer.PlaybackState != PlaybackState.Stopped)
            {
                channel.audioPlayer.Stop();
                channel.audioPlayer.Position = TimeSpan.Zero;
                colorSlider1.Value = 0;
            }
        }

        private void trackbarVolume_Scroll(object sender, ScrollEventArgs e)
        {

        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedFile == null)
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "WAV |*.wav;";
            sfd.FileName = Path.GetFileNameWithoutExtension(selectedFile.Title) + ".wav";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                selectedFile.GetCurrentChannel().audioStream.WriteToFile(sfd.FileName);
            }
        }
    }
}
