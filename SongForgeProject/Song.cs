using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicCreate
{
    public class Song : INotifyPropertyChanged
    {
        private string _filePath;
        private string _fileName;
        private string _fileNameWithExtension;
        private string _extension;
        private TimeSpan _duration;
        private bool _isFavorite;
        private bool _isPlaying;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string FileNameWithExtension
        {
            get => _fileNameWithExtension;
            set
            {
                _fileNameWithExtension = value;
                OnPropertyChanged(nameof(FileNameWithExtension));
            }
        }
        public string Extension
        {
            get => _extension;
            set
            {
                _extension = value;
                OnPropertyChanged(nameof(Extension));
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                OnPropertyChanged(nameof(Duration));
            }
        }
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                _isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
            }
        }
        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying)); // Обновляем UI при изменении
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
