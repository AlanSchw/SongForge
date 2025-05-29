using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicCreate
{
    public class Project : INotifyPropertyChanged
    {
        private string _name;
        private TimeSpan _duration;
        private int _bpm;
        private DateTime _dateTimeCreate;
        private DateTime _dateTimeChange;
        private string _projectPath;
        private bool _isFavorite;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
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

        public int Bpm
        {
            get => _bpm;
            set
            {
                _bpm = value;
                OnPropertyChanged(nameof(Bpm));
            }
        }

        public DateTime Date_time_create
        {
            get => _dateTimeCreate;
            set
            {
                _dateTimeCreate = value;
                OnPropertyChanged(nameof(Date_time_create));
            }
        }

        public DateTime Date_time_change
        {
            get => _dateTimeChange;
            set
            {
                _dateTimeChange = value;
                OnPropertyChanged(nameof(Date_time_change));
            }
        }

        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                _projectPath = value;
                OnPropertyChanged(nameof(ProjectPath));
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
