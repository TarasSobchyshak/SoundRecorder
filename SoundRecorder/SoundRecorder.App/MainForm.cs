using NAudio.Lame;
using NAudio.Wave;
using System;
using System.IO;
using System.Windows.Forms;

namespace SoundRecorder.App
{
    public partial class MainForm : Form
    {
        private WasapiLoopbackCapture _systemAudio;
        private WaveInEvent _microphone;
        private BufferedWaveProvider _systemAudioBuffer;
        private BufferedWaveProvider _microphoneBuffer;
        private MixingWaveProvider32 _mixer;
        private bool _isRecording = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                _systemAudio = new WasapiLoopbackCapture();
                _microphone = new WaveInEvent();
                _microphone.WaveFormat = _systemAudio.WaveFormat;

                _systemAudioBuffer = new BufferedWaveProvider(_systemAudio.WaveFormat);
                _systemAudioBuffer.BufferDuration = TimeSpan.FromMinutes(30); // adjust buffer as needed

                _microphoneBuffer = new BufferedWaveProvider(_microphone.WaveFormat);
                _microphoneBuffer.BufferDuration = TimeSpan.FromMinutes(30); // adjust buffer as needed

                _mixer = new MixingWaveProvider32(new IWaveProvider[] { _systemAudioBuffer, _microphoneBuffer });

                _systemAudio.DataAvailable += SystemAudio_DataAvailable;
                _microphone.DataAvailable += Microphone_DataAvailable;

                _systemAudio.StartRecording();
                _microphone.StartRecording();

                _isRecording = true;
                btnStartStop.Text = "Stop";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting recording: " + ex.Message);
            }
        }

        private void StopRecording()
        {
            try
            {
                _isRecording = false;

                _systemAudio.DataAvailable -= SystemAudio_DataAvailable;
                _microphone.DataAvailable -= Microphone_DataAvailable;

                _systemAudio?.StopRecording();
                _microphone?.StopRecording();

                string filePath = saveFileDialog.FileName;

                if (!string.IsNullOrEmpty(filePath))
                {
                    //// Save to WAV
                    //SaveToWav(filePath);

                    // Save to MP3
                    SaveToMp3(filePath);
                }

                _systemAudio?.Dispose();
                _systemAudio = null;

                _microphone?.Dispose();
                _microphone = null;

                btnStartStop.Text = "Start";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error stopping recording: " + ex.Message);
            }
        }

        //private void SaveToWav(string filePath)
        //{
        //    try
        //    {
        //        if (_mixer == null || string.IsNullOrEmpty(filePath))
        //            return;

        //        using (var writer = new WaveFileWriter(Path.ChangeExtension(filePath, ".wav"), _mixer.WaveFormat))
        //        {
        //            var buffer = new byte[_mixer.WaveFormat.BlockAlign * 1000]; // Adjust buffer size as needed
        //            int bytesRead;
        //            long bytesWritten = 0;
        //            long maxBytes = CalculateMaxBytes(_systemAudioBuffer, _microphoneBuffer);

        //            while (bytesWritten < maxBytes)
        //            {
        //                bytesRead = _mixer.Read(buffer, 0, buffer.Length);
        //                if (bytesRead <= 0)
        //                    break;

        //                writer.Write(buffer, 0, bytesRead);
        //                bytesWritten += bytesRead;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("Error saving WAV file: " + ex.Message);
        //    }
        //}

        private void SaveToMp3(string filePath)
        {
            try
            {
                if (_mixer == null || string.IsNullOrEmpty(filePath))
                    return;

                using (var mp3File = new FileStream(Path.ChangeExtension(filePath, ".mp3"), FileMode.Create))
                {
                    using (var mp3Writer = new LameMP3FileWriter(mp3File, _mixer.WaveFormat, 128))
                    {
                        var buffer = new byte[_mixer.WaveFormat.BlockAlign * 1000]; // Adjust buffer size as needed
                        int bytesRead;
                        long bytesWritten = 0;
                        long maxBytes = CalculateMaxBytes(_systemAudioBuffer, _microphoneBuffer);

                        while (bytesWritten < maxBytes)
                        {
                            bytesRead = _mixer.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0)
                                break;

                            mp3Writer.Write(buffer, 0, bytesRead);  
                            bytesWritten += bytesRead;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving MP3 file: " + ex.Message);
            }
        }

        private long CalculateMaxBytes(BufferedWaveProvider systemAudioBuffer, BufferedWaveProvider microphoneBuffer)
        {
            if (systemAudioBuffer == null || microphoneBuffer == null)
                return 0;

            // Calculate max bytes based on the combined durations of system audio and microphone
            long maxBytesSystem = (long)systemAudioBuffer.BufferedDuration.TotalSeconds * systemAudioBuffer.WaveFormat.AverageBytesPerSecond;
            long maxBytesMicrophone = (long)microphoneBuffer.BufferedDuration.TotalSeconds * microphoneBuffer.WaveFormat.AverageBytesPerSecond;

            // Return the greater of the two max byte calculations
            return Math.Max(maxBytesSystem, maxBytesMicrophone);
        }

        private void SystemAudio_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_isRecording)
                return;

            _systemAudioBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void Microphone_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_isRecording)
                return;

            // Check if there's enough space in the buffer before adding samples
            if (_microphoneBuffer.BufferLength - _microphoneBuffer.BufferedBytes >= e.BytesRecorded)
            {
                _microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
            else
            {
                // Handle buffer full situation (e.g., log, notify user, adjust buffer size)
                MessageBox.Show("Microphone buffer full. Adjusting buffer size may be required.");
            }
        }
    }
}
