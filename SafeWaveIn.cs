using NAudio.Wave;
using System;
using System.Diagnostics;

namespace WalkieTalkieApp
{
    public class SafeWaveIn : IDisposable
    {
        private WaveInEvent waveIn;
        private bool isRecording = false;
        private readonly object lockObj = new object();

        public SafeWaveIn()
        {
            waveIn = new WaveInEvent();
            // Valor por defecto: 44100 Hz, 1 canal
            waveIn.WaveFormat = new WaveFormat(44100, 1);
        }

        // Se expone la propiedad WaveFormat para poder configurarla desde fuera
        public WaveFormat WaveFormat
        {
            get => waveIn.WaveFormat;
            set => waveIn.WaveFormat = value;
        }

        public event EventHandler<WaveInEventArgs>? DataAvailable
        {
            add => waveIn.DataAvailable += value;
            remove => waveIn.DataAvailable -= value;
        }

        public void StartRecording()
        {
            lock (lockObj)
            {
                if (!isRecording)
                {
                    try
                    {
                        waveIn.StartRecording();
                        isRecording = true;
                    }
                    catch (Exception ex)
                    {
                        throw new AudioDeviceException("Error al iniciar grabación", ex);
                    }
                }
            }
        }

        public void StopRecording()
        {
            lock (lockObj)
            {
                if (isRecording)
                {
                    try
                    {
                        waveIn.StopRecording();
                        isRecording = false;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error al detener grabación: {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (lockObj)
            {
                waveIn?.Dispose();
                waveIn = null!;
            }
        }
    }

    public class AudioDeviceException : Exception
    {
        public AudioDeviceException(string message, Exception inner)
            : base(message, inner) { }
    }
}