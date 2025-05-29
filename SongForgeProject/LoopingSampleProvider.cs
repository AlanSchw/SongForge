using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicCreate
{
    public class LoopingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly AudioFileReader reader;
        private long currentSample;
        private readonly long totalSamples;

        public LoopingSampleProvider(ISampleProvider source)
        {
            this.source = source;
            this.reader = source as AudioFileReader;
            if (this.reader != null)
            {
                this.totalSamples = this.reader.Length / this.reader.WaveFormat.BlockAlign;
            }
            else
            {
                this.totalSamples = long.MaxValue; // Для не-AudioFileReader источников
            }
            this.currentSample = 0;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalSamplesRead = 0;

            while (totalSamplesRead < count)
            {
                int samplesToRead = count - totalSamplesRead;
                int samplesRead = source.Read(buffer, offset + totalSamplesRead, samplesToRead);

                if (samplesRead == 0 && reader != null)
                {
                    // Сбрасываем позицию в начало файла
                    reader.Position = 0;
                    currentSample = 0;
                    if (totalSamplesRead == 0)
                    {
                        // Если ничего не прочитано, выходим, чтобы избежать бесконечного цикла
                        break;
                    }
                }
                else if (samplesRead == 0)
                {
                    // Для источников, не поддерживающих сброс
                    break;
                }

                totalSamplesRead += samplesRead;
                currentSample += samplesRead;
            }

            return totalSamplesRead;
        }
    }
}
