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
    public sealed class BWAV : VGAdudioFile, IEditor<AudioPlayerPanel>, IFileFormat
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

        private static AudioData Decode(byte[] bytes)
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
            int sampleRate = 0;
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
            }

            return ReadWave(new MemoryStream(CreateWave(channels, sampleRate), false));
        }

        private static byte[] Encode(AudioData input)
        {
            if (input == null) throw new InvalidOperationException("No audio data is loaded.");
            IAudioFormat source = input.GetAllFormats().FirstOrDefault();
            if (source == null || source.ChannelCount < 1 || source.ChannelCount > 64)
                throw new InvalidDataException("The audio data has no supported channels.");
            Pcm16Format pcm = source.ToPcm16();
            int sampleCount = pcm.SampleCount;
            if (sampleCount < 1 || sampleCount > int.MaxValue - 14) throw new InvalidDataException("The audio sample count is invalid.");
            int sampleRate = source.SampleRate;
            if (sampleRate < 1 || sampleRate > 384000) throw new InvalidDataException("The audio sample rate is invalid.");

            int count = pcm.Channels.Length;
            var encoded = new byte[count][];
            var coefficients = new short[count][];
            var offsets = new uint[count];
            uint position = Align((uint)(0x10 + count * 0x4c), 0x40);
            for (int channel = 0; channel < count; channel++)
            {
                short[] samples = pcm.Channels[channel];
                coefficients[channel] = GcAdpcmCoefficients.CalculateCoefficients(samples);
                encoded[channel] = GcAdpcmEncoder.Encode(samples, coefficients[channel]);
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
                    writer.Write((ushort)(count == 1 ? 2 : (channel == 0 ? 0 : channel == 1 ? 1 : 2)));
                    writer.Write((uint)sampleRate);
                    writer.Write((uint)sampleCount);
                    writer.Write((uint)sampleCount);
                    foreach (short coefficient in coefficients[channel]) writer.Write(coefficient);
                    writer.Write(offsets[channel]); // offset in the original, full wave
                    writer.Write(offsets[channel]); // offset in this file
                    writer.Write((uint)0);          // looping disabled
                    writer.Write(uint.MaxValue);    // loop end sentinel
                    writer.Write((uint)0);          // loop start
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
