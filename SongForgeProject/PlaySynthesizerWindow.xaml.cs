using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NAudio.Wave;
using NAudio.Midi;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MusicCreate
{
    /// <summary>
    /// Логика взаимодействия для PlaySynthesizerWindow.xaml
    /// </summary>
    public partial class PlaySynthesizerWindow : Window
    {
        public Project project;

        private WaveOutEvent waveOut;
        private SineWaveProvider sineWaveProvider;
        private MidiIn midiIn;
        private MidiOut midiOut;
        private bool isRecording;
        private bool isPlaying;
        private DispatcherTimer playbackTimer;
        private DateTime recordingStartTime;
        private ObservableCollection<MidiNote> tempNotes; // For recording
        private double playbackPosition; // In seconds</midinote>

        public ObservableCollection<KeyModel> Keys { get; set; } = new ObservableCollection<KeyModel>();
        public ObservableCollection<MidiNote> MidiNotes { get; set; } = new ObservableCollection<MidiNote>();
        public ObservableCollection<MeasureModel> Measures { get; set; } = new ObservableCollection<MeasureModel>();
        public ObservableCollection<KeyModel> WhiteKeys => new ObservableCollection<KeyModel>(Keys.Where(k => !k.IsBlackKey && !k.IsPianoRollKey));
        public ObservableCollection<KeyModel> BlackKeys => new ObservableCollection<KeyModel>(Keys.Where(k => k.IsBlackKey && !k.IsPianoRollKey));
        public ObservableCollection<KeyModel> WhiteKeys_PianoRoll => new ObservableCollection<KeyModel>(Keys.Where(k => !k.IsBlackKey && k.IsPianoRollKey));
        public ObservableCollection<KeyModel> BlackKeys_PianoRoll => new ObservableCollection<KeyModel>(Keys.Where(k => k.IsBlackKey && k.IsPianoRollKey));
        public PlaySynthesizerWindow(Project project)
        {
            InitializeComponent();
            this.project = project;
            this.DataContext = this.project;
            this.Loaded += PlaySynthesizerWindow_Loaded;
            GenerateKeys();
            GenerateKeys_PianoRoll();
            GenerateMeasures();
            InitializeAudio();
            InitializeMidi();
            tempNotes = new ObservableCollection<MidiNote>();
            playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            playbackTimer.Tick += PlaybackTimer_Tick;
        }
        private void InitializeAudio()
        {
            sineWaveProvider = new SineWaveProvider();
            waveOut = new WaveOutEvent();
            waveOut.Init(sineWaveProvider);
            waveOut.Play();
        }
        private void InitializeMidi()
        {
            try
            {
                if (MidiIn.NumberOfDevices > 0)
                {
                    midiIn = new MidiIn(0);
                    midiIn.MessageReceived += MidiIn_MessageReceived;
                    midiIn.Start();
                }
                if (MidiOut.NumberOfDevices > 0)
                {
                    midiOut = new MidiOut(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void GenerateMeasures()
        {
            for (int i = 0; i < 100; i++) // 100 measures
            {
                Measures.Add(new MeasureModel { MeasureNumber = i + 1, PositionX = i * 50 });
            }
        }
        private void PlaySynthesizerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (scrollViewer.ScrollableWidth > 0)
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.ScrollableWidth / 2);
            }
            if (scrollViewerPianoRoll.ScrollableHeight > 0)
            {
                scrollViewerPianoRoll.ScrollToVerticalOffset(scrollViewerPianoRoll.ScrollableHeight / 2);
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            waveOut?.Dispose();
            midiIn?.Dispose();
            midiOut?.Dispose();
            Close();
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            isRecording = !isRecording;
            if (isRecording)
            {
                MidiNotes.Clear();
                tempNotes.Clear();
                recordingStartTime = DateTime.Now;
                //RecordBtn.Content = "Stop Recording";
            }
            else
            {
                foreach (var note in tempNotes)
                {
                    MidiNotes.Add(note);
                }
                tempNotes.Clear();
                //RecordBtn.Content = "Record";
            }
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            isPlaying = false;
            playbackTimer.Stop();
            sineWaveProvider.StopAll();
            playbackPosition = 0;
            //PlayBtn.Content = "Play";
        }

        private void MetronomeBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void pause_btn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void refresh_btn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog { Filter = "MIDI Files|*.mid" };
            if (saveDialog.ShowDialog() == true)
            {
                SaveMidiFile(saveDialog.FileName);
            }
        }
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog { Filter = "MIDI Files|*.mid" };
            if (openDialog.ShowDialog() == true)
            {
                LoadMidiFile(openDialog.FileName);
            }
        }

        private void skip_left_btn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void skip_right_btn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void play_btn_Click(object sender, RoutedEventArgs e)
        {
            if (!isPlaying)
            {
                isPlaying = true;
                playbackPosition = 0;
                playbackTimer.Start();
                //PlayBtn.Content = "Pause";
            }
            else
            {
                isPlaying = false;
                playbackTimer.Stop();
                sineWaveProvider.StopAll();
                //PlayBtn.Content = "Play";
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void KeyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button keyButton && keyButton.DataContext is KeyModel keyModel)
            {
                ApplyKeyAnimation(keyButton, keyModel.IsBlackKey);
                int midiNote = NoteNameToMidiNumber(keyModel.NoteName);
                sineWaveProvider.PlayNote(midiNote);
                if (midiOut != null)
                {
                    midiOut.Send(MidiMessage.StartNote(midiNote, 127, 1).RawData);
                }
                if (isRecording)
                {
                    double startTime = (DateTime.Now - recordingStartTime).TotalSeconds;
                    tempNotes.Add(new MidiNote
                    {
                        Note = midiNote,
                        StartTime = startTime,
                        Duration = 0.25 // Placeholder, updated on note off
                    });
                }
            }
        }
        private void MidiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
            {
                sineWaveProvider.PlayNote(noteOn.NoteNumber);
                if (isRecording)
                {
                    double startTime = (DateTime.Now - recordingStartTime).TotalSeconds;
                    tempNotes.Add(new MidiNote
                    {
                        Note = noteOn.NoteNumber,
                        StartTime = startTime,
                        Duration = 0.25
                    });
                }
            }
        }
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            playbackPosition += playbackTimer.Interval.TotalSeconds;
            foreach (var note in MidiNotes)
            {
                if (Math.Abs(note.StartTime - playbackPosition) < 0.01)
                {
                    sineWaveProvider.PlayNote(note.Note, note.Duration);
                    if (midiOut != null)
                    {
                        midiOut.Send(MidiMessage.StartNote(note.Note, 127, 1).RawData);
                    }
                }
            }
        }
        private void SaveMidiFile(string filePath)
        {
            try
            {
                var midiEvents = new MidiEventCollection(1, 480); // Type 1 MIDI, 480 ticks per quarter note
                var track = new List<MidiEvent>();

                foreach (var note in MidiNotes)
                {
                    long startTick = (long)(note.StartTime * 480); // Convert seconds to ticks
                    long durationTicks = (long)(note.Duration * 480);
                    track.Add(new NoteOnEvent(startTick, 1, note.Note, 127, 0));
                    track.Add(new NoteEvent(startTick + durationTicks, 1, MidiCommandCode.NoteOff, note.Note, 0));
                }

                // Add EndTrack meta event
                track.Add(new MetaEvent(MetaEventType.EndTrack, 0, track.Last().AbsoluteTime + 1));
                midiEvents.AddTrack(track);
                MidiFile.Export(filePath, midiEvents);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save MIDI file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadMidiFile(string filePath)
        {
            MidiNotes.Clear();
            var midiFile = new MidiFile(filePath);
            foreach (var track in midiFile.Events)
            {
                foreach (var midiEvent in track)
                {
                    if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
                    {
                        var noteOff = track.FirstOrDefault(e => e is NoteEvent ne && ne.NoteNumber == noteOn.NoteNumber && ne.CommandCode == MidiCommandCode.NoteOff && e.AbsoluteTime > noteOn.AbsoluteTime) as NoteEvent;
                        if (noteOff != null)
                        {
                            double startTime = noteOn.AbsoluteTime / 480.0;
                            double duration = (noteOff.AbsoluteTime - noteOn.AbsoluteTime) / 480.0;
                            MidiNotes.Add(new MidiNote
                            {
                                Note = noteOn.NoteNumber,
                                StartTime = startTime,
                                Duration = duration
                            });
                        }
                    }
                }
            }
        }
        private void MidiNote_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb noteThumb && noteThumb.DataContext is MidiNote midiNote)
            {
                midiNote.StartTime = Math.Max(0, midiNote.StartTime + (e.HorizontalChange / 50));
                midiNote.Note = Math.Min(127, Math.Max(0, midiNote.Note - (int)(e.VerticalChange / 30)));
            }
        }

        public void GenerateKeys_PianoRoll()
        {
            string[] whiteKeys = { "C", "D", "E", "F", "G", "A", "B" };
            string[] blackKeys = { "C#", "D#", "", "F#", "G#", "A#" };

            int octaveCount = 7; // Количество октав
            double whiteKeyHeight = 30;
            double blackKeyOffset = 20;

            Keys.Add(new KeyModel { NoteName = "A0", IsBlackKey = false, IsPianoRollKey = true, PositionY = (127 - NoteNameToMidiNumber("A0")) * 30 });
            Keys.Add(new KeyModel { NoteName = "A#0", IsBlackKey = true, IsPianoRollKey = true, Margin = new Thickness(0, blackKeyOffset, 0, 0), PositionY = (127 - NoteNameToMidiNumber("A#0")) * 30 });
            Keys.Add(new KeyModel { NoteName = "B0", IsBlackKey = false, IsPianoRollKey = true, PositionY = (127 - NoteNameToMidiNumber("B0")) * 30 });

            for (int octave = 0; octave < octaveCount; octave++)
            {
                for (int i = 0; i < whiteKeys.Length; i++)
                {
                    string note = whiteKeys[i] + (octave + 1);
                    Keys.Add(new KeyModel { NoteName = note, IsBlackKey = false, IsPianoRollKey = true, PositionY = (127 - NoteNameToMidiNumber(note)) * 30 });

                    // Добавляем черные клавиши между белыми (кроме E-F и B-C)
                    if (i < blackKeys.Length && !string.IsNullOrEmpty(blackKeys[i]))
                    {
                        string blackNote = blackKeys[i] + (octave + 1);
                        Keys.Add(new KeyModel
                        {
                            NoteName = blackNote,
                            IsBlackKey = true,
                            IsPianoRollKey = true,
                            Margin = new Thickness(0, (octave * 210) + ((i + 2) * whiteKeyHeight + blackKeyOffset), 0, 0),
                            PositionY = (127 - NoteNameToMidiNumber(blackNote)) * 30
                        });
                    }
                }
            }
            Keys.Add(new KeyModel { NoteName = "C8", IsBlackKey = false, IsPianoRollKey = true, PositionY = (127 - NoteNameToMidiNumber("C8")) * 30 });
        }
        public void GenerateKeys()
        {
            string[] whiteKeys = { "C", "D", "E", "F", "G", "A", "B" };
            string[] blackKeys = { "C#", "D#", "", "F#", "G#", "A#" };

            int octaveCount = 7; // Количество октав
            double whiteKeyWidth = 50;
            double blackKeyOffset = 35;

            Keys.Add(new KeyModel { NoteName = "A0", IsBlackKey = false, IsPianoRollKey = false });
            Keys.Add(new KeyModel { NoteName = "A#0", IsBlackKey = true, IsPianoRollKey = false, Margin = new Thickness(blackKeyOffset, 0, 0, 0) });
            Keys.Add(new KeyModel { NoteName = "B0", IsBlackKey = false, IsPianoRollKey = false });

            for (int octave = 0; octave < octaveCount; octave++)
            {
                for (int i = 0; i < whiteKeys.Length; i++)
                {
                    string note = whiteKeys[i] + (octave + 1);
                    Keys.Add(new KeyModel { NoteName = note, IsBlackKey = false, IsPianoRollKey = false });


                    // Добавляем черные клавиши между белыми (кроме E-F и B-C)
                    if (i < blackKeys.Length && !string.IsNullOrEmpty(blackKeys[i]))
                    {
                        string blackNote = blackKeys[i] + (octave + 1);
                        Keys.Add(new KeyModel
                        {
                            NoteName = blackNote,
                            IsBlackKey = true,
                            IsPianoRollKey = false,
                            Margin = new Thickness((octave * 350) + ((i + 2) * whiteKeyWidth + blackKeyOffset), 0, 0, 0)
                        });
                    }
                }
            }
            Keys.Add(new KeyModel { NoteName = "C8", IsBlackKey = false, IsPianoRollKey = false });
        }
        private void ApplyKeyAnimation(Button keyButton, bool isBlackKey)
        {
            Color fromColor = isBlackKey ? Colors.Black : Colors.White;
            Color toColor = isBlackKey ? Colors.Gray : Color.FromRgb(211, 204, 197);

            var colorAnimation = new ColorAnimation
            {
                From = fromColor,
                To = toColor,
                Duration = new Duration(TimeSpan.FromSeconds(0.1)),
                AutoReverse = true
            };

            var brush = new SolidColorBrush(fromColor);
            keyButton.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
        }
        private int NoteNameToMidiNumber(string noteName)
        {
            string note = noteName.Substring(0, noteName.Length - 1);
            int octave = int.Parse(noteName.Substring(noteName.Length - 1));
            int noteIndex = Array.IndexOf(new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }, note);
            return noteIndex + (octave + 1) * 12;
        }
        //private void MidiNote_DragDelta(object sender, DragDeltaEventArgs e)
        //{
        //    if (sender is Thumb noteThumb && noteThumb.DataContext is MidiNote midiNote)
        //    {
        //        midiNote.StartTime += (int)(e.HorizontalChange / 50); // Привязка к тактам
        //        midiNote.Note -= (int)(e.VerticalChange / 3); // Привязка к нотам
        //    }
        //}

    }

    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class SineWaveProvider : ISampleProvider
    {
        private readonly Dictionary<int, double> activeNotes = new Dictionary<int, double>();
        private readonly WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        private const double BaseFrequency = 440.0; // A4

        public WaveFormat WaveFormat => waveFormat;

        public void PlayNote(int midiNote, double duration = 0.25)
        {
            double frequency = BaseFrequency * Math.Pow(2, (midiNote - 69) / 12.0);
            activeNotes[midiNote] = frequency;
            if (duration > 0)
            {
                Task.Delay((int)(duration * 1000)).ContinueWith(_ => StopNote(midiNote));
            }
        }

        public void StopNote(int midiNote)
        {
            activeNotes.Remove(midiNote);
        }

        public void StopAll()
        {
            activeNotes.Clear();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                double sample = 0;
                foreach (var frequency in activeNotes.Values)
                {
                    sample += 0.3 * Math.Sin(2 * Math.PI * frequency * (i + offset) / waveFormat.SampleRate);
                }
                buffer[offset + i] = (float)sample;
            }
            return count;
        }
    }
    public class KeyModel
    {
        public string NoteName { get; set; }
        public bool IsBlackKey { get; set; }
        public bool IsPianoRollKey {  get; set; }
        public Thickness Margin { get; set; }
        public double PositionY { get; set; }
    }
    public class MidiNote
    {
        public int Note { get; set; }
        public double StartTime { get; set; } // In seconds
        public double Duration { get; set; } // In seconds
        public double PositionX => StartTime * 50; // 1 second = 50 pixels
        public double PositionY => (127 - Note) * 30; // 1 note = 30 pixels
        public double DurationWidth => Duration * 50;
    }

    public class MeasureModel
    {
        public int MeasureNumber { get; set; }
        public double PositionX { get; set; }
    }
}
