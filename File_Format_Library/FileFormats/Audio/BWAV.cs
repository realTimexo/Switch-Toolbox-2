using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Toolbox;
using Toolbox.Library;
using Toolbox.Library.Forms;
using VGAudio.Codecs.GcAdpcm;
using VGAudio.Containers.Wave;
using VGAudio.Formats;
using VGAudio.Formats.Pcm16;

namespace FirstPlugin
{
    /// <summary>
    /// Reader/writer for Nintendo AAL Binary Wave files (BWAV).
    /// BWAV is distinct from NintendoWare BFWAV and stores each channel as DSP-ADPCM.
    /// </summary>
    public sealed class BWAV : VGAdudioFile, IEditor<AudioPlayerPanel>, IFileFormat, IBinaryWaveSettings
    {
        private sealed class ChannelInfo
        {
            public int SampleRate;
            public int SampleCount;
            public short[] Coefficients;
            public uint DataOffset;
            public short History1;
            public short History2;
        }

        public FileType FileType { get; set; } = FileType.Audio;
        public bool CanSave { get; set; } = true;
        public string[] Description { get; set; } = new[] { "Nintendo AAL Binary Wave" };
        public string[] Extension { get; set; } = new[] { "*.bwav" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }
        public int TargetSampleRate { get; set; } = 48000;
        public int TargetChannelCount { get; set; } = 2;
        public int[] TargetChannelPans { get; set; } = new[] { 0, 1 };
        public bool TargetLooping { get; set; }
        public uint TargetLoopStartSample { get; set; }
        public uint TargetLoopEndSample { get; set; }
        public uint SourceSampleCount { get; private set; }

        public bool Identify(Stream stream)
        {
            if (stream == null || !stream.CanRead || stream.Length - stream.Position < 4)
                return false;
            long position = stream.Position;
            try
            {
                byte[] magic = new byte[4];
                return stream.Read(magic, 0, magic.Length) == magic.Length &&
                    magic[0] == (byte)'B' && magic[1] == (byte)'W' &&
                    magic[2] == (byte)'A' && magic[3] == (byte)'V';
            }
            finally { stream.Position = position; }
        }

        public Type[] Types { get { return new Type[0]; } }

        public AudioPlayerPanel OpenForm()
        {
            var panel = new AudioPlayerPanel { Text = FileName, Dock = DockStyle.Fill };
            panel.LoadFile(audioData, this);
            return panel;
        }

        public void FillEditor(UserControl control) { ((AudioPlayerPanel)control).LoadFile(audioData, this); }
        public void Unload() { }

        public bool EditSettings(IWin32Window owner)
        {
            using (var dialog = new Form())
            using (var layout = new TableLayoutPanel())
            using (var sampleRate = new NumericUpDown())
            using (var channels = new ComboBox())
            using (var loop = new CheckBox())
            using (var loopStart = new NumericUpDown())
            using (var loopEnd = new NumericUpDown())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = "BWAV export settings";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new System.Drawing.Size(420, 310);
                layout.Dock = DockStyle.Fill;
                layout.Padding = new Padding(12);
                layout.ColumnCount = 2;
                layout.RowCount = 8;
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                sampleRate.Minimum = 8000;
                sampleRate.Maximum = 192000;
                sampleRate.Increment = 1000;
                sampleRate.Value = Math.Max(sampleRate.Minimum, Math.Min(sampleRate.Maximum, TargetSampleRate));
                channels.DropDownStyle = ComboBoxStyle.DropDownList;
                channels.Items.Add("Mono");
                channels.Items.Add("Stereo");
                channels.SelectedIndex = TargetChannelCount == 1 ? 0 : 1;
                loop.Text = "Enable loop points";
                loop.Checked = TargetLooping;
                loopStart.Minimum = 0;
                loopStart.Maximum = uint.MaxValue;
                loopStart.Value = Math.Min((decimal)uint.MaxValue, TargetLoopStartSample);
                loopEnd.Minimum = 0;
                loopEnd.Maximum = uint.MaxValue;
                uint initialEnd = TargetLoopEndSample;
                if (initialEnd == uint.MaxValue || initialEnd == 0) initialEnd = SourceSampleCount;
                loopEnd.Value = Math.Min((decimal)uint.MaxValue, initialEnd);

                AddSettingsRow(layout, 0, "Sample rate (Hz)", sampleRate);
                AddSettingsRow(layout, 1, "Channel layout", channels);
                AddSettingsRow(layout, 2, "Codec", new Label { Text = "DSP-ADPCM (BWAV standard)", AutoSize = true, Anchor = AnchorStyles.Left });
                AddSettingsRow(layout, 3, "Playback backend", new Label { Text = "Not stored in BWAV; uses selected Windows output", AutoSize = true, Anchor = AnchorStyles.Left });
                AddSettingsRow(layout, 4, "Loop", loop);
                AddSettingsRow(layout, 5, "Loop start (samples)", loopStart);
                AddSettingsRow(layout, 6, "Loop end (samples)", loopEnd);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                layout.Controls.Add(buttons, 0, 7);
                layout.SetColumnSpan(buttons, 2);
                dialog.Controls.Add(layout);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
                if (loop.Checked)
                {
                    if (loopEnd.Value <= loopStart.Value || loopEnd.Value > int.MaxValue)
                    {
                        MessageBox.Show(owner, "Loop end must be greater than loop start and fit within the audio sample range.",
                            "BWAV settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    var source = audioData == null ? null : audioData.GetAllFormats().FirstOrDefault();
                    long outputSamples = source == null ? SourceSampleCount :
                        (long)Math.Round((double)source.SampleCount * (double)sampleRate.Value / source.SampleRate);
                    if (outputSamples <= 0 || loopEnd.Value > outputSamples)
                    {
                        MessageBox.Show(owner, "Loop end exceeds the converted audio duration.",
                            "BWAV settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }

                TargetSampleRate = (int)sampleRate.Value;
                TargetChannelCount = channels.SelectedIndex == 0 ? 1 : 2;
                TargetChannelPans = TargetChannelCount == 1 ? new[] { 2 } : new[] { 0, 1 };
                TargetLooping = loop.Checked;
                TargetLoopStartSample = (uint)loopStart.Value;
                TargetLoopEndSample = (uint)loopEnd.Value;
                if (audioData != null)
                {
                    if (TargetLooping)
                        audioData.SetLoop(true, checked((int)TargetLoopStartSample), checked((int)TargetLoopEndSample));
                    else
                        audioData.SetLoop(false);
                }
                return true;
            }
        }

        private static void AddSettingsRow(TableLayoutPanel layout, int row, string title, Control value)
        {
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            value.Dock = DockStyle.Fill;
            layout.Controls.Add(value, 1, row);
        }

        public void Load(Stream stream)
        {
            if (stream == null || !stream.CanRead) throw new ArgumentException("A readable BWAV stream is required.", nameof(stream));
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                audioData = Decode(copy.ToArray());
            }
            Format = this;
            CanSave = true;
        }

        public void Save(Stream stream)
        {
            if (stream == null || !stream.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(stream));
            var targetFormat = Format ?? this;
            bool exportWave = targetFormat.Extension.Any(ext => ext.Equals("*.wav", StringComparison.OrdinalIgnoreCase));
            byte[] data = exportWave
                ? new WaveWriter().GetFile(audioData, waveConfiguration)
                : Encode(audioData);
            stream.Write(data, 0, data.Length);
        }

        public static AudioData ReadWave(Stream wavStream)
        {
            if (wavStream == null || !wavStream.CanRead) throw new ArgumentException("A readable WAV stream is required.", nameof(wavStream));
            wavStream.Position = 0;
            return new WaveReader().ReadWithConfig(wavStream).Audio;
        }

        private AudioData Decode(byte[] bytes)
        {
            if (bytes.Length < 0x10 || bytes[0] != 'B' || bytes[1] != 'W' || bytes[2] != 'A' || bytes[3] != 'V')
                throw new InvalidDataException("The file does not contain a valid BWAV header.");

            bool littleEndian;
            if (bytes[4] == 0xff && bytes[5] == 0xfe) littleEndian = true;
            else if (bytes[4] == 0xfe && bytes[5] == 0xff) littleEndian = false;
            else throw new InvalidDataException("The BWAV byte-order marker is not recognized.");

            int channelCount = ReadUInt16(bytes, 14, littleEndian);
            if (channelCount < 1 || channelCount > 64 || bytes.Length < 0x10L + channelCount * 0x4cL)
                throw new InvalidDataException("The BWAV channel table is invalid or truncated.");

            var channels = new short[channelCount][];
            var pans = new int[channelCount];
            int sampleRate = 0;
            uint sampleCount = 0;
            for (int i = 0; i < channelCount; i++)
            {
                int info = 0x10 + i * 0x4c;
                int rate = checked((int)ReadUInt32(bytes, info + 4, littleEndian));
                int count = checked((int)ReadUInt32(bytes, info + 0x0c, littleEndian));
                uint offset = ReadUInt32(bytes, info + 0x34, littleEndian);
                if (rate < 1 || rate > 384000 || count < 0 || count > 100000000)
                    throw new InvalidDataException("A BWAV channel declares an invalid sample rate or sample count.");
                int encodedLength = checked(((count + 13) / 14) * 8);
                if (offset > bytes.Length || encodedLength > bytes.Length - (long)offset)
                    throw new InvalidDataException("A BWAV channel points outside the file.");

                pans[i] = ReadUInt16(bytes, info + 2, littleEndian);
                var coefficients = new short[16];
                for (int c = 0; c < coefficients.Length; c++)
                    coefficients[c] = ReadInt16(bytes, info + 0x10 + c * 2, littleEndian);
                short history1 = ReadInt16(bytes, info + 0x46, littleEndian);
                short history2 = ReadInt16(bytes, info + 0x48, littleEndian);
                var adpcm = new byte[encodedLength];
                Buffer.BlockCopy(bytes, (int)offset, adpcm, 0, encodedLength);
                channels[i] = GcAdpcmDecoder.Decode(adpcm, coefficients, new GcAdpcmParameters
                {
                    SampleCount = count,
                    History1 = history1,
                    History2 = history2
                });
                if (sampleRate == 0) sampleRate = rate;
                else if (rate != sampleRate) throw new InvalidDataException("BWAV channels with different sample rates cannot be played together.");
                if (i == 0) sampleCount = (uint)count;
            }

            TargetSampleRate = sampleRate;
            TargetChannelCount = channelCount;
            TargetChannelPans = pans;
            SourceSampleCount = sampleCount;
            uint loopEnd = ReadUInt32(bytes, 0x10 + 0x3c, littleEndian);
            uint loopStart = ReadUInt32(bytes, 0x10 + 0x40, littleEndian);
            bool loopFlag = ReadUInt32(bytes, 0x10 + 0x38, littleEndian) != 0;
            uint normalizedLoopEnd = loopEnd == uint.MaxValue ? sampleCount : loopEnd;
            TargetLooping = loopFlag && loopStart < normalizedLoopEnd && normalizedLoopEnd <= sampleCount;
            TargetLoopStartSample = TargetLooping ? loopStart : 0;
            TargetLoopEndSample = TargetLooping ? normalizedLoopEnd : sampleCount;

            AudioData decoded = ReadWave(new MemoryStream(CreateWave(channels, sampleRate), false));
            if (TargetLooping)
                decoded.SetLoop(true, checked((int)TargetLoopStartSample), checked((int)TargetLoopEndSample));
            return decoded;
        }

        private byte[] Encode(AudioData input)
        {
            if (input == null) throw new InvalidOperationException("No audio data is loaded.");
            IAudioFormat source = input.GetAllFormats().FirstOrDefault();
            if (source == null || source.ChannelCount < 1 || source.ChannelCount > 64)
                throw new InvalidDataException("The audio data has no supported channels.");
            Pcm16Format pcm = source.ToPcm16();
            int sourceSampleCount = pcm.SampleCount;
            if (sourceSampleCount < 1 || sourceSampleCount > int.MaxValue - 14) throw new InvalidDataException("The audio sample count is invalid.");
            int sourceRate = source.SampleRate;
            int sampleRate = TargetSampleRate > 0 ? TargetSampleRate : sourceRate;
            if (sampleRate < 1 || sampleRate > 384000) throw new InvalidDataException("The audio sample rate is invalid.");

            int count = TargetChannelCount == 1 || TargetChannelCount == 2 ? TargetChannelCount : pcm.ChannelCount;
            if (count < 1 || count > 2) throw new InvalidDataException("BWAV supports one or two channels.");
            short[][] outputChannels = ConvertChannels(pcm.Channels, count, sampleRate, sourceRate);
            int sampleCount = outputChannels[0].Length;
            if (sampleCount < 1 || sampleCount > 100000000) throw new InvalidDataException("The converted audio sample count exceeds the supported BWAV limit.");
            var encoded = new byte[count][];
            var coefficients = new short[count][];
            var offsets = new uint[count];
            uint position = Align((uint)(0x10 + count * 0x4c), 0x40);
            for (int channel = 0; channel < count; channel++)
            {
                short[] samples = outputChannels[channel];
                coefficients[channel] = GcAdpcmCoefficients.CalculateCoefficients(samples);
                encoded[channel] = GcAdpcmEncoder.Encode(samples, coefficients[channel]);
                int requiredLength = checked(((sampleCount + 13) / 14) * 8);
                if (encoded[channel].Length < requiredLength)
                    Array.Resize(ref encoded[channel], requiredLength);
                offsets[channel] = position;
                position = Align(checked(position + (uint)encoded[channel].Length), 0x40);
            }

            using (var output = new MemoryStream(checked((int)(offsets[count - 1] + (uint)encoded[count - 1].Length))))
            using (var writer = new BinaryWriter(output))
            {
                writer.Write(new[] { (byte)'B', (byte)'W', (byte)'A', (byte)'V' });
                writer.Write((ushort)0xfeff); // little-endian BOM marker
                writer.Write((ushort)1);      // version 0.1
                writer.Write((uint)0);        // CRC is filled after writing channel data
                writer.Write((ushort)0);      // not a prefetch file
                writer.Write((ushort)count);
                for (int channel = 0; channel < count; channel++)
                {
                    writer.Write((ushort)1);
                    int pan = TargetChannelPans != null && channel < TargetChannelPans.Length
                        ? TargetChannelPans[channel] : (count == 1 ? 2 : channel);
                    writer.Write((ushort)(pan >= 0 && pan <= 2 ? pan : (count == 1 ? 2 : channel)));
                    writer.Write((uint)sampleRate);
                    writer.Write((uint)sampleCount);
                    writer.Write((uint)sampleCount);
                    foreach (short coefficient in coefficients[channel]) writer.Write(coefficient);
                    writer.Write(offsets[channel]); // offset in the original, full wave
                    writer.Write(offsets[channel]); // offset in this file
                    bool loop = TargetLooping && TargetLoopStartSample < TargetLoopEndSample && TargetLoopEndSample <= sampleCount;
                    writer.Write(loop ? (uint)1 : 0u);
                    writer.Write(loop ? TargetLoopEndSample : uint.MaxValue);
                    writer.Write(loop ? TargetLoopStartSample : 0u);
                    writer.Write((ushort)0);        // predictor/scale
                    writer.Write((short)0);          // history sample 1
                    writer.Write((short)0);          // history sample 2
                    writer.Write((ushort)0);
                }
                for (int channel = 0; channel < count; channel++)
                {
                    while (output.Position < offsets[channel]) writer.Write((byte)0);
                    writer.Write(encoded[channel]);
                }
                writer.Flush();
                byte[] result = output.ToArray();
                uint crc = ComputeCrc(encoded);
                WriteUInt32(result, 8, crc);
                return result;
            }
        }

        private static short[][] ConvertChannels(short[][] source, int targetCount, int targetRate, int sourceRate)
        {
            if (source == null || source.Length == 0 || source.Any(x => x == null || x.Length == 0))
                throw new InvalidDataException("The source audio contains an empty channel.");
            int sourceCount = source[0].Length;
            if (source.Any(x => x.Length != sourceCount))
                throw new InvalidDataException("All source channels must have the same sample count.");

            var mapped = new short[targetCount][];
            if (targetCount == 1 && source.Length > 1)
            {
                mapped[0] = new short[sourceCount];
                for (int i = 0; i < sourceCount; i++)
                {
                    long sum = 0;
                    for (int c = 0; c < source.Length; c++) sum += source[c][i];
                    mapped[0][i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, sum / source.Length));
                }
            }
            else
            {
                for (int c = 0; c < targetCount; c++)
                    mapped[c] = source[Math.Min(c, source.Length - 1)];
            }

            if (sourceRate == targetRate) return mapped;
            long outputCountLong = ((long)sourceCount * targetRate + sourceRate / 2) / sourceRate;
            if (outputCountLong < 1 || outputCountLong > 100000000)
                throw new InvalidDataException("The resampled audio duration is out of range.");
            int outputCount = (int)outputCountLong;
            var resampled = new short[targetCount][];
            for (int c = 0; c < targetCount; c++)
            {
                resampled[c] = new short[outputCount];
                for (int i = 0; i < outputCount; i++)
                {
                    double sourcePosition = (double)i * sourceRate / targetRate;
                    int left = Math.Min((int)sourcePosition, sourceCount - 1);
                    int right = Math.Min(left + 1, sourceCount - 1);
                    double fraction = sourcePosition - left;
                    double value = mapped[c][left] + (mapped[c][right] - mapped[c][left]) * fraction;
                    resampled[c][i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)Math.Round(value)));
                }
            }
            return resampled;
        }

        private static byte[] CreateWave(short[][] channels, int sampleRate)
        {
            int channelCount = channels.Length;
            int samples = channels[0].Length;
            int dataLength = checked(samples * channelCount * 2);
            using (var stream = new MemoryStream(checked(44 + dataLength)))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)channelCount);
                writer.Write(sampleRate); writer.Write(checked(sampleRate * channelCount * 2));
                writer.Write((ushort)(channelCount * 2)); writer.Write((ushort)16);
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                writer.Write(dataLength);
                for (int sample = 0; sample < samples; sample++)
                    for (int channel = 0; channel < channelCount; channel++) writer.Write(channels[channel][sample]);
                writer.Flush(); return stream.ToArray();
            }
        }

        private static uint ComputeCrc(byte[][] channels)
        {
            uint crc = 0xffffffff;
            foreach (byte[] channel in channels)
                foreach (byte value in channel)
                {
                    crc ^= value;
                    for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
                }
            return ~crc;
        }

        private static uint Align(uint value, uint alignment) => (value + alignment - 1) & ~(alignment - 1);
        private static ushort ReadUInt16(byte[] data, int offset, bool little) => little
            ? (ushort)(data[offset] | data[offset + 1] << 8) : (ushort)(data[offset] << 8 | data[offset + 1]);
        private static short ReadInt16(byte[] data, int offset, bool little) => unchecked((short)ReadUInt16(data, offset, little));
        private static uint ReadUInt32(byte[] data, int offset, bool little) => little
            ? (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24)
            : (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24);
        }
    }
}
