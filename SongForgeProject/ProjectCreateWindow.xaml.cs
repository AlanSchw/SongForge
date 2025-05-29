using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NAudio;
using System.ComponentModel;
using Microsoft.Win32;
using System.IO;
using NAudio.Wave;
using NAudio.Lame;
using NAudio.FileFormats;
using NAudio.CoreAudioApi;
using System.Drawing;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using NAudio.Wave.SampleProviders;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Media;
using System.Threading;
using System.Timers;
using System.Windows.Controls.Primitives;
using NAudio.Dsp;
using System.Collections.Concurrent;
using System.Diagnostics;
using MusicCreate.Properties;
using NAudio.Wave.WaveFormats;

namespace MusicCreate
{
    /// <summary>
    /// Логика взаимодействия для ProjectCreateWindow.xaml
    /// </summary>
    public partial class ProjectCreateWindow : Window
    {
        private DoubleAnimation animation;
        private bool isPlaying = false;
        public Project project;
        WaveIn waveIn; // WaveIn - поток для записи
        WaveFileWriter writer; // Класс для записи в файл
        string outputFilename = "имя_файла.wav"; // Имя файла для записи (будет обновляться)
        Polyline audioLine; // Используется Polyline, чтобы создать визуализацию волны
        Canvas canvas;
        private PointCollection recordedPoints = new PointCollection(); // Поле, чтобы хранить точки визуализации звуковой волны
        ObservableCollection<ListBoxItem> items = new ObservableCollection<ListBoxItem>(); // ObservableCollection - источник данных для ListBox1
        List<ListBox> LB_Track_List = new List<ListBox>();
        List<string> TrackNames = new List<string>();
        public int TotalBeats = 0; // Общее количество тактов в проекте (задается с метрономом: сколько раз метроном ударил)
        public double step = 200; // Ширина одного такта
        List<Line> BeatsLineList = new List<Line>();
        private Dictionary<string, Polyline> trackWaveforms = new Dictionary<string, Polyline>();

        private WaveOutEvent metronomeOut;
        private SoundPlayer metronomePlayer;
        private System.Windows.Threading.DispatcherTimer metronomeTimer;
        private bool isMetronomeOn = false;

        private long totalBytesRecorded = 0; // Общее количество записанных байтов
        private DispatcherTimer timer;
        private Dictionary<int, Canvas> trackCanvases = new Dictionary<int, Canvas>();
        private DispatcherTimer updateTimer;
        private PointCollection tempPoints = new PointCollection(); // Временное хранилище для новых точек
        //private WaveOutEvent waveOut; // Глобальная переменная для управления воспроизведением
        private AudioFileReader audioFile; // Для чтения файла
        private bool isLooping = false; // Флаг для зацикленного воспроизведения

        private MixingSampleProvider mixer; // Для микширования треков
        private List<string> trackFiles = new List<string>(); // Список путей к файлам треков

        private TimeSpan maxTrackDuration; // Максимальная длина трека

        private List<AudioFileReader> readers; // Для хранения читателей треков

        private List<List<string>> tracks = new List<List<string>>(); // Список дорожек, каждая дорожка - список файлов
        private List<bool> mutedTracks = new List<bool>(); // true - дорожка отключена, false - включена

        private Dictionary<int, float> trackVolumes = new Dictionary<int, float>(); // Громкость каждой дорожки (0.0f - 1.0f)
        private List<VolumeSampleProvider> volumeProviders = new List<VolumeSampleProvider>(); // Провайдеры громкости для дорожек
        private float masterVolume = 1.0f; // Общая громкость микса
        private VolumeSampleProvider masterVolumeProvider; // Провайдер общей громкости

        private double originalRow2Height; // Для хранения исходной высоты ScrollViewer

        // Команда для обработки пробела (воспроизведение/остановка записи)
        public ICommand PlayOrStopRecordingCommand { get; private set; }

        // Команда для сохранения (Ctrl+S)
        public ICommand SaveCommand { get; private set; }

        // Команда для удаления дорожки (Delete)
        public ICommand DeleteTrackCommand { get; private set; }

        // Флаг, указывающий, идёт ли запись
        private bool isRecording = false;

        // Путь к временной папке для треков
        private string temporaryTracksFolder;
        // Путь к папке проекта (если проект сохранён)
        private string projectFolder;
        // Флаг, указывающий, был ли проект сохранён
        private bool isProjectSaved = false;
        // Имя проекта
        private string projectName;

        private IWavePlayer waveOut; // Поддерживает WasapiOut

        public static int SelectedInputDeviceIndex { get; set; } = 0;
        public static int SelectedOutputDeviceIndex { get; set; } = 0;
        public static bool UseAsio { get; set; } = false;
        public static string AsioDriverName { get; set; } = null;



        private BufferedWaveProvider bufferProvider;
        private DispatcherTimer scrollTimer;


        // Поля для WriteableBitmap
        private WriteableBitmap waveformBitmap;
        private List<float> waveformSamples = new List<float>(); // Хранилище всех сэмплов
        private int bitmapWidth = 1000; // Начальная ширина битмапа
        private int bitmapHeight = 90; // Высота битмапа (соответствует высоте Canvas)
        private double pixelsPerSample = 0.5; // Пикселей на сэмпл (для масштабирования)
        private double compressionFactor = 0.1; // Коэффициент сжатия волны
        private ConcurrentQueue<float[]> audioDataQueue = new ConcurrentQueue<float[]>();
        private CancellationTokenSource visualizationCts = new CancellationTokenSource();
        private int lastRenderedSampleCount = 0; // Для частичного обновления






        //private WaveInEvent waveIn;
        private int bandCount = 4; // Количество полос (как в изображении)
        private float[] bandValues;

        private AudioSpectrum spectrum;

        public ProjectCreateWindow(Project project)
        {
            InitializeComponent();
            this.project = project;
            this.DataContext = this.project;
            ListBox1.ItemsSource = items;

            // Инициализация пути к временной папке треков
            temporaryTracksFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects", "TemporaryTracks");
            if (!Directory.Exists(temporaryTracksFolder))
            {
                Directory.CreateDirectory(temporaryTracksFolder);
            }

            // Если проект уже существует, загружаем его
            if (!string.IsNullOrEmpty(project.ProjectPath))
            {
                LoadProject();
            }
            else
            {
                for (int i = 0; i < 5; i++) DrawGridLines();
            }

            // Инициализация таймера
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromSeconds(2); // Обновление раз в 2 секунды
            updateTimer.Tick += (s, e) =>
            {
                if (trackWaveforms.ContainsKey(outputFilename) && tempPoints.Count > 0)
                {
                    trackWaveforms[outputFilename].Points = new PointCollection(recordedPoints);
                    tempPoints.Clear();
                }
            };

            // Инициализация таймера для скроллинга
            scrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1)
            };
            scrollTimer.Tick += ScrollTimer_Tick;


            // Сохраняем исходную высоту второго ряда
            originalRow2Height = MainGrid.RowDefinitions[1].ActualHeight;
            // Подписываемся на событие изменения размера окна
            SizeChanged += ProjectCreateWindow_SizeChanged;

            // Подписываемся на событие закрытия окна
            Closing += ProjectCreateWindow_Closing;

            // Добавляем обработчик PreviewKeyDown для горячих клавиш
            PreviewKeyDown += ProjectCreateWindow_PreviewKeyDown;

            save_btn.Opacity = 0.5;


            spectrum = new AudioSpectrum(spectrumCanvas);
            //waveformRenderer = new RealTimeWaveform(waveformImage);


            // Загружаем настройки при запуске
            SelectedInputDeviceIndex = Properties.Settings.Default.InputDeviceIndex;
            SelectedOutputDeviceIndex = Properties.Settings.Default.OutputDeviceIndex;
            UseAsio = Properties.Settings.Default.UseAsio;
            AsioDriverName = Properties.Settings.Default.AsioDriverName;

            System.Diagnostics.Debug.WriteLine($"Загружены настройки: Ввод={SelectedInputDeviceIndex}, Вывод={SelectedOutputDeviceIndex}, ASIO={UseAsio}, Драйвер={AsioDriverName}");
        }

        private void ImportAudioFile()
        {
            try
            {
                if (ListBox1.SelectedIndex == -1)
                {
                    MessageBox.Show("Выберите дорожку для импорта аудиофайла", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                // Открываем диалоговое окно для выбора файла
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Wave Files (*.wav)|*.wav|All Files (*.*)|*.*",
                    Title = "Выберите аудиофайл для импорта"
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    return; // Пользователь отменил выбор
                }

                int selectedTrack = ListBox1.SelectedIndex;

                // Убедимся, что коллекция tracks содержит достаточно дорожек
                while (tracks.Count <= selectedTrack)
                {
                    tracks.Add(new List<string>());
                }

                // Формируем имя файла по системе track{trackNumber}-{index}.wav
                string fileName;
                for (int i = 1; ; i++)
                {
                    fileName = $"track{selectedTrack + 1}-{i}.wav";
                    outputFilename = System.IO.Path.Combine(temporaryTracksFolder, fileName);
                    if (!File.Exists(outputFilename))
                    {
                        break;
                    }
                }

                // Копируем файл в TemporaryTracks
                //File.Copy(openFileDialog.FileName, outputFilename, true);
                using (var reader = new MediaFoundationReader(openFileDialog.FileName))
                using (var waveFileWriter = new WaveFileWriter(outputFilename, reader.WaveFormat))
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveFileWriter.Write(buffer, 0, bytesRead);
                    }
                }

                // Создаем новый Canvas для дорожки
                Canvas newCanvas = new Canvas
                {
                    Background = Brushes.White,
                    Height = 90,
                    Width = 10,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0),
                    Tag = outputFilename
                };

                // Добавляем Canvas в выбранную дорожку
                for (int i = 0; i < ListBox1.Items.Count; i++)
                {
                    if (selectedTrack == i)
                    {
                        LB_Track_List[i].Items.Add(newCanvas);
                        canvas = newCanvas;
                        trackCanvases[selectedTrack + 1] = newCanvas;
                        tracks[selectedTrack].Add(outputFilename);
                        break;
                    }
                }

                // Отображаем волну для импортированного файла
                VisualizeWaveform(outputFilename, newCanvas);

                save_btn.Opacity = 1; // Активируем кнопку сохранения
                Debug.WriteLine($"Аудиофайл импортирован: {outputFilename}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте аудиофайла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VisualizeWaveform(string filePath, Canvas canvas)
        {
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    double sampleRate = reader.WaveFormat.SampleRate;
                    float[] samples = new float[reader.Length / 4];
                    int samplesRead = reader.Read(samples, 0, samples.Length);

                    int samplesPerPixel = (int)Math.Max(1, sampleRate * 10 * pixelsPerSample / 44100);
                    int peakCount = samplesRead / samplesPerPixel;
                    double requiredWidth = peakCount * pixelsPerSample * compressionFactor;
                    requiredWidth = Math.Max(requiredWidth, 300);

                    bitmapWidth = (int)Math.Ceiling(requiredWidth);
                    waveformBitmap = new WriteableBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Rgb24, null);

                    // Инициализируем белый фон
                    byte[] pixels = new byte[bitmapWidth * bitmapHeight * 3];
                    for (int i = 0; i < pixels.Length; i += 3)
                    {
                        pixels[i] = 255; // R
                        pixels[i + 1] = 255; // G
                        pixels[i + 2] = 255; // B
                    }

                    int centerY = bitmapHeight / 2;
                    float amplificationFactor = 2.0f;

                    for (int i = 0; i < peakCount; i++)
                    {
                        int startSample = i * samplesPerPixel;
                        int endSample = Math.Min(startSample + samplesPerPixel, samplesRead);

                        float max = float.MinValue;
                        float min = float.MaxValue;

                        for (int j = startSample; j < endSample; j++)
                        {
                            float sample = samples[j];
                            if (sample > max) max = sample;
                            if (sample < min) min = sample;
                        }

                        max = Math.Min(1.0f, Math.Max(-1.0f, max * amplificationFactor));
                        min = Math.Min(1.0f, Math.Max(-1.0f, min * amplificationFactor));

                        double x = i * pixelsPerSample * compressionFactor;
                        int pixelX = (int)x;
                        if (pixelX >= bitmapWidth) continue;

                        int maxY = centerY - (int)(max * (bitmapHeight / 2));
                        int minY = centerY - (int)(min * (bitmapHeight / 2));

                        for (int y = Math.Min(maxY, minY); y <= Math.Max(maxY, minY); y++)
                        {
                            if (y >= 0 && y < bitmapHeight)
                            {
                                int index = (y * bitmapWidth + pixelX) * 3;
                                pixels[index] = 0;     // R
                                pixels[index + 1] = 0; // G
                                pixels[index + 2] = 255; // B (синий)
                            }
                        }
                    }

                    waveformBitmap.WritePixels(new Int32Rect(0, 0, bitmapWidth, bitmapHeight), pixels, bitmapWidth * 3, 0);

                    var waveformImage = new Image { Source = waveformBitmap };
                    Canvas.SetZIndex(waveformImage, 1);
                    canvas.Children.Clear();
                    canvas.Children.Add(waveformImage);

                    canvas.Width = requiredWidth;
                    ListBoxItem parentItem = FindParentListBoxItem(canvas);
                    if (parentItem != null)
                    {
                        parentItem.Width = requiredWidth;
                    }

                    trackWaveforms[filePath] = null;
                    Debug.WriteLine($"Визуализация создана для файла: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка визуализации: {ex.Message}");
            }
        }

        private void InitializeWaveformBitmap(Canvas targetCanvas)
        {
            bitmapWidth = 1000; // Сбрасываем ширину для новой записи
            waveformBitmap = new WriteableBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Rgb24, null);
            // Заполняем фон белым
            byte[] pixels = new byte[bitmapWidth * bitmapHeight * 3];
            for (int i = 0; i < pixels.Length; i += 3)
            {
                pixels[i] = 255; // R
                pixels[i + 1] = 255; // G
                pixels[i + 2] = 255; // B
            }
            waveformBitmap.WritePixels(new Int32Rect(0, 0, bitmapWidth, bitmapHeight), pixels, bitmapWidth * 3, 0);

            var waveformImage = new Image { Source = waveformBitmap };
            Canvas.SetZIndex(waveformImage, 1);
            targetCanvas.Children.Clear(); // Очищаем Canvas перед добавлением
            targetCanvas.Children.Add(waveformImage);
            trackWaveforms[outputFilename] = null;
        }
        private void LoadProject()
        {
            try
            {
                string projectFilePath = project.ProjectPath;
                if (!File.Exists(projectFilePath))
                {
                    MessageBox.Show($"Файл проекта не найден: {projectFilePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                isProjectSaved = true;
                projectFolder = System.IO.Path.GetDirectoryName(projectFilePath);
                projectName = System.IO.Path.GetFileNameWithoutExtension(projectFilePath);

                string[] projectLines = File.ReadAllLines(projectFilePath);
                if (projectLines.Length == 0)
                {
                    MessageBox.Show("Файл проекта пуст!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string[] firstLineParts = projectLines[0].Split(',');
                if (firstLineParts.Length < 3)
                {
                    MessageBox.Show("Некорректный формат первой строки файла проекта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                TimeSpan mixDuration = TimeSpan.ParseExact(firstLineParts[0], @"hh\:mm\:ss\:fff", CultureInfo.InvariantCulture);
                int tempo = int.Parse(firstLineParts[1]);

                TextBox tempoTextBox = FindName("TempoTextBox") as TextBox;
                if (tempoTextBox != null)
                {
                    tempoTextBox.Text = tempo.ToString();
                }

                items.Clear();
                tracks.Clear();
                mutedTracks.Clear();
                trackVolumes.Clear();
                volumeProviders.Clear();
                LB_Track_List.Clear();
                TrackNames.Clear();
                trackWaveforms.Clear();
                trackFiles.Clear();

                string projectTracksFolder = System.IO.Path.Combine(projectFolder, "Tracks");
                for (int i = 1; i < projectLines.Length; i++)
                {
                    string[] trackParts = projectLines[i].Split(',');
                    if (trackParts.Length < 6)
                    {
                        System.Diagnostics.Debug.WriteLine($"Некорректный формат строки дорожки {i}: {projectLines[i]}");
                        continue;
                    }

                    string trackName = trackParts[0];
                    string instrument = trackParts[1];
                    TimeSpan trackDuration = TimeSpan.ParseExact(trackParts[2], @"hh\:mm\:ss\:fff", CultureInfo.InvariantCulture);
                    string muteState = trackParts[3];
                    float trackVolumePercent = float.Parse(trackParts[4], CultureInfo.InvariantCulture);
                    string[] trackFileNames = trackParts[5].Split(';').Where(f => !string.IsNullOrEmpty(f)).ToArray();

                    List<string> trackFilesList = new List<string>();
                    foreach (var fileName in trackFileNames)
                    {
                        string filePath = System.IO.Path.Combine(projectTracksFolder, fileName);
                        if (File.Exists(filePath))
                        {
                            trackFilesList.Add(filePath);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Файл трека не найден: {filePath}");
                        }
                    }

                    // Добавляем дорожку, даже если trackFilesList пуст
                    AddTrackFromProject(trackName, instrument, muteState == "mute", trackVolumePercent / 100f, trackFilesList.ToArray());
                    System.Diagnostics.Debug.WriteLine($"Дорожка добавлена: {trackName}, файлов: {trackFilesList.Count}");
                }

                int beatsCount = (int)Math.Ceiling(mixDuration.TotalSeconds / (60.0 / tempo));
                for (int i = 0; i < beatsCount; i++)
                {
                    DrawGridLines();
                }

                System.Diagnostics.Debug.WriteLine($"Проект загружен: {projectFilePath}, треков: {tracks.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке проекта: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private void AddTrackFromProject(string trackName, string instrument, bool isMuted, float trackVolume, string[] trackFileNames)
        {
            // Создаём ListBoxItem для дорожки
            ListBoxItem item1 = new ListBoxItem();
            item1.Padding = new Thickness(0);
            StackPanel stackPanel1 = new StackPanel();
            stackPanel1.Orientation = Orientation.Horizontal;
            stackPanel1.Height = 100;

            Grid grid1 = new Grid();
            grid1.Width = 190; // 170
            grid1.Background = new SolidColorBrush(Colors.LightSkyBlue);
            grid1.MouseRightButtonDown += TrackGrid_MouseRightButtonDown;

            // Создаем контекстное меню
            ContextMenu contextMenu = new ContextMenu();
            if (instrument != "Recording")
            {
                // Создаем пункт меню "Открыть инструмент"
                MenuItem openInstrumentItem = new MenuItem();
                openInstrumentItem.Header = "Открыть инструмент";
                openInstrumentItem.Click += (sender, e) => OpenInstrument(sender, e, instrument);
                // Добавляем пункт в контекстное меню
                contextMenu.Items.Add(openInstrumentItem);
            }
            // Привязываем контекстное меню к элементу Grid
            grid1.ContextMenu = contextMenu;

            // Ползунок громкости
            Slider volumeSlider = new Slider
            {
                Height = 90,
                Value = trackVolume, // Устанавливаем громкость из файла
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = tracks.Count // Индекс дорожки
            };
            volumeSlider.ValueChanged += VolumeSlider_ValueChanged;

            // Название инструмента
            TextBlock tb1 = new TextBlock();
            tb1.Text = instrument;
            tb1.Margin = new Thickness(100, 20, 5, 0);

            // Изображение инструмента
            string instrumentImagePath = GetInstrumentImagePath(instrument);
            Image image1 = new Image();
            image1.HorizontalAlignment = HorizontalAlignment.Left;
            image1.VerticalAlignment = VerticalAlignment.Center;
            image1.Width = 70;
            image1.Height = 70;
            image1.Margin = new Thickness(25, 10, 0, 10); // (20, 10, 0, 10);
            if (!string.IsNullOrEmpty(instrumentImagePath))
            {
                image1.Source = new BitmapImage(new Uri(instrumentImagePath));
            }

            // Название трека
            TextBox tb2 = new TextBox();
            tb2.Text = trackName;
            tb2.Margin = new Thickness(100, 40, 5, 40); // (90, 40, 5, 40)
            tb2.TextChanged += Tb2_TextChanged;
            tb2.KeyDown += TextBox_KeyDown;

            // Кнопки действий с треком
            // Удалить
            Button delete_btn = new Button();
            Image image_del = new Image();
            image_del.Source = new BitmapImage(new Uri("pack://application:,,,/Images/icon-dustbin.png"));
            delete_btn.Content = image_del;
            delete_btn.Width = 25;
            delete_btn.HorizontalAlignment = HorizontalAlignment.Left;
            delete_btn.VerticalAlignment = VerticalAlignment.Bottom;
            delete_btn.Margin = new Thickness(100, 0, 0, 10);
            delete_btn.Background = null;
            delete_btn.BorderBrush = null;
            delete_btn.ToolTip = "Удалить трек";
            delete_btn.Click += DeleteTrackBtn_Click;

            // Кнопка "Mute"
            Button mute_btn = new Button();
            Image image_mute = new Image();
            image_mute.Source = new BitmapImage(new Uri(isMuted ? "pack://application:,,,/Images/icon-mute.png" : "pack://application:,,,/Images/icon-unmute.png"));
            mute_btn.Content = image_mute;
            mute_btn.Width = 25;
            mute_btn.HorizontalAlignment = HorizontalAlignment.Left;
            mute_btn.VerticalAlignment = VerticalAlignment.Bottom;
            mute_btn.Margin = new Thickness(130, 0, 0, 10);
            mute_btn.Padding = new Thickness(-2);
            mute_btn.Background = null;
            mute_btn.BorderBrush = null;
            mute_btn.ToolTip = "Выключить/включить трек";
            mute_btn.Click += MuteTrackBtn_Click;
            mute_btn.Tag = tracks.Count; // Привязываем индекс дорожки

            grid1.Children.Add(volumeSlider);
            grid1.Children.Add(tb1);
            grid1.Children.Add(image1);
            grid1.Children.Add(tb2);
            grid1.Children.Add(delete_btn);
            grid1.Children.Add(mute_btn);
            stackPanel1.Children.Add(grid1);

            ListBox LB_Track1 = new ListBox();
            LB_Track1.Background = null;
            LB_Track1.Padding = new Thickness(0);
            // Создаем ItemsPanelTemplate
            ItemsPanelTemplate itemsPanelTemplate = new ItemsPanelTemplate();
            var stackPanelFactory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            stackPanelFactory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Horizontal);
            itemsPanelTemplate.VisualTree = stackPanelFactory;
            LB_Track1.ItemsPanel = itemsPanelTemplate;
            LB_Track_List.Add(LB_Track1);
            stackPanel1.Children.Add(LB_Track1);

            item1.Content = stackPanel1;
            items.Add(item1);

            // Добавляем новую дорожку в tracks
            List<string> trackFilesList = new List<string>();

            string projectTracksFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(project.ProjectPath), "Tracks");
            foreach (var fileName in trackFileNames)
            {
                string filePath = System.IO.Path.Combine(projectTracksFolder, fileName);
                if (File.Exists(filePath))
                {
                    trackFilesList.Add(filePath);
                    // Создаём Canvas для файла и добавляем визуализацию
                    Canvas newCanvas = new Canvas
                    {
                        Background = Brushes.White,
                        Height = 90,
                        Width = 10, // Начальная ширина, будет обновлена
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 0),
                        Tag = filePath
                    };
                    LB_Track1.Items.Add(newCanvas);

                    // Восстанавливаем визуализацию звуковой волны
                    RestoreWaveformVisualization(filePath, newCanvas);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Файл трека не найден: {filePath}");
                }
            }

            tracks.Add(trackFilesList);
            mutedTracks.Add(isMuted);
            trackVolumes[tracks.Count - 1] = trackVolume;
            volumeProviders.Add(null); // Пустой провайдер, заполнится при воспроизведении
            TrackNames.Add(trackName);

            // Обновляем высоту линий тактов
            for (int i = 0; i < BeatsLineList.Count; i++)
            {
                UpdateLineHeight(BeatsLineList[i]);
            }
        }

        private string GetInstrumentImagePath(string instrument)
        {
            // Здесь логика для получения пути к изображению инструмента
            Dictionary<string, string> instrumentImages = new Dictionary<string, string>
            {
                { "Recording", "pack://application:,,,/Images/Instruments/icon-mic.png" },
                { "Synthesizer", "pack://application:,,,/Images/Instruments/icon-synthesizer.png" },
                { "Drums", "pack://application:,,,/Images/Instruments/icon-drum-set.png" },
                { "Electric guitar", "pack://application:,,,/Images/Instruments/icon-electric-guitar.png" },
                { "Acoustic guitar", "pack://application:,,,/Images/Instruments/icon-guitar.png" },
                { "Bass", "pack://application:,,,/Images/Instruments/icon-bass.png" }
            };

            return instrumentImages.ContainsKey(instrument) ? instrumentImages[instrument] : "";
        }

        private void RestoreWaveformVisualization(string filePath, Canvas canvas)
        {
            try
            {
                VisualizeWaveform(filePath, canvas);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка восстановления: {ex.Message}");
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportAudioFile();
        }

        private bool _isClosingConfirmed = false; // Флаг для отслеживания подтверждения закрытия
        private bool _navigateToMainWindow = false; // Флаг для определения, нужно ли открыть MainWindow
        // Общий метод для обработки закрытия окна
        private void HandleWindowClosing(bool navigateToMainWindow = false)
        {
            // Если закрытие уже подтверждено, не вмешиваемся
            if (_isClosingConfirmed)
            {
                return;
            }

            // Устанавливаем флаг, нужно ли открыть MainWindow (для кнопки "Назад")
            _navigateToMainWindow = navigateToMainWindow;

            // Останавливаем запись
            StopRecording();

            // Если проект не сохранён (save_btn.Opacity == 1), показываем сообщение
            if (save_btn.Opacity == 1)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    "Сохранить проект перед закрытием?",
                    "Внимание",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    // Вызываем сохранение проекта
                    try
                    {
                        SaveButton_Click(this, new RoutedEventArgs());
                        System.Diagnostics.Debug.WriteLine("Проект успешно сохранён");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении проекта: {ex.Message}");
                        MessageBox.Show($"Ошибка при сохранении проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // Не продолжаем закрытие, если сохранение не удалось
                    }
                }
                else if (messageBoxResult == MessageBoxResult.No)
                {
                    // Пользователь отказался от сохранения, продолжаем закрытие
                }
                else
                {
                    // Если пользователь закрыл MessageBox (например, нажал Esc), не продолжаем закрытие
                    return;
                }
            }

            // Подтверждаем закрытие
            _isClosingConfirmed = true;

            // Выполняем очистку
            PerformCleanup();

            // Если нужно открыть MainWindow (для кнопки "Назад")
            if (_navigateToMainWindow)
            {
                MainWindow mw = new MainWindow();
                mw.Show();
            }

            // Инициируем закрытие окна
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Window window = this;
                window.Close();
            }));
        }

        // Метод для выполнения очистки ресурсов
        //private void PerformCleanup()
        //{
        //    // Останавливаем и освобождаем waveOut
        //    if (waveOut != null)
        //    {
        //        try
        //        {
        //            waveOut.Stop();
        //            waveOut.Dispose();
        //            System.Diagnostics.Debug.WriteLine("waveOut успешно остановлен и освобождён");
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"Ошибка при остановке waveOut: {ex.Message}");
        //        }
        //        waveOut = null;
        //    }

        //    // Освобождаем audioFile
        //    if (audioFile != null)
        //    {
        //        try
        //        {
        //            audioFile.Dispose();
        //            System.Diagnostics.Debug.WriteLine("audioFile успешно освобождён");
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении audioFile: {ex.Message}");
        //        }
        //        audioFile = null;
        //    }

        //    // Останавливаем метроном
        //    if (metronomeOut != null)
        //    {
        //        try
        //        {
        //            metronomeOut.Stop();
        //            metronomeOut.Dispose();
        //            System.Diagnostics.Debug.WriteLine("metronomeOut успешно остановлен и освобождён");
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"Ошибка при остановке metronomeOut: {ex.Message}");
        //        }
        //        metronomeOut = null;
        //    }

        //    // Очищаем readers
        //    if (readers != null)
        //    {
        //        foreach (var reader in readers)
        //        {
        //            try
        //            {
        //                reader?.Dispose();
        //                System.Diagnostics.Debug.WriteLine("AudioFileReader успешно освобождён");
        //            }
        //            catch (Exception ex)
        //            {
        //                System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении AudioFileReader: {ex.Message}");
        //            }
        //        }
        //        readers.Clear();
        //    }

        //    // Удаляем все файлы из TemporaryTracks
        //    if (Directory.Exists(temporaryTracksFolder))
        //    {
        //        try
        //        {
        //            // Получаем список файлов
        //            var files = Directory.GetFiles(temporaryTracksFolder);
        //            System.Diagnostics.Debug.WriteLine($"Найдено файлов в TemporaryTracks: {files.Length}");

        //            foreach (var file in files)
        //            {
        //                try
        //                {
        //                    File.Delete(file);
        //                    System.Diagnostics.Debug.WriteLine($"Удалён файл из TemporaryTracks: {file}");
        //                }
        //                catch (Exception ex)
        //                {
        //                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении файла {file}: {ex.Message}");
        //                }
        //            }

        //            // Проверяем, остались ли файлы
        //            if (Directory.GetFiles(temporaryTracksFolder).Length == 0)
        //            {
        //                Directory.Delete(temporaryTracksFolder, true);
        //                System.Diagnostics.Debug.WriteLine("Папка TemporaryTracks успешно удалена");
        //            }
        //            else
        //            {
        //                System.Diagnostics.Debug.WriteLine("Не удалось удалить TemporaryTracks: папка не пуста");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"Ошибка при удалении TemporaryTracks: {ex.Message}");
        //        }
        //    }
        //    else
        //    {
        //        System.Diagnostics.Debug.WriteLine("Папка TemporaryTracks не существует");
        //    }
        //}
        private void PerformCleanup()
        {
            // Останавливаем и освобождаем waveOut
            if (waveOut != null)
            {
                try
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    System.Diagnostics.Debug.WriteLine("waveOut успешно остановлен и освобождён");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при остановке waveOut: {ex.Message}");
                }
                waveOut = null;
            }

            // Освобождаем bufferProvider
            if (bufferProvider != null)
            {
                try
                {
                    bufferProvider.ClearBuffer();
                    System.Diagnostics.Debug.WriteLine("bufferProvider успешно очищен");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при очистке bufferProvider: {ex.Message}");
                }
                bufferProvider = null;
            }

            // Освобождаем audioFile
            if (audioFile != null)
            {
                try
                {
                    audioFile.Dispose();
                    System.Diagnostics.Debug.WriteLine("audioFile успешно освобождён");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении audioFile: {ex.Message}");
                }
                audioFile = null;
            }

            // Останавливаем метроном
            if (metronomeOut != null)
            {
                try
                {
                    metronomeOut.Stop();
                    metronomeOut.Dispose();
                    System.Diagnostics.Debug.WriteLine("metronomeOut успешно остановлен и освобождён");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при остановке metronomeOut: {ex.Message}");
                }
                metronomeOut = null;
            }

            // Очищаем readers
            if (readers != null)
            {
                foreach (var reader in readers)
                {
                    try
                    {
                        reader?.Dispose();
                        System.Diagnostics.Debug.WriteLine("AudioFileReader успешно освобождён");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении AudioFileReader: {ex.Message}");
                    }
                }
                readers.Clear();
            }

            // Удаляем все файлы из TemporaryTracks
            if (Directory.Exists(temporaryTracksFolder))
            {
                try
                {
                    // Получаем список файлов
                    var files = Directory.GetFiles(temporaryTracksFolder);
                    System.Diagnostics.Debug.WriteLine($"Найдено файлов в TemporaryTracks: {files.Length}");

                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"Удалён файл из TemporaryTracks: {file}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при удалении файла {file}: {ex.Message}");
                        }
                    }

                    // Проверяем, остались ли файлы
                    if (Directory.GetFiles(temporaryTracksFolder).Length == 0)
                    {
                        Directory.Delete(temporaryTracksFolder, true);
                        System.Diagnostics.Debug.WriteLine("Папка TemporaryTracks успешно удалена");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Не удалось удалить TemporaryTracks: папка не пуста");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении TemporaryTracks: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Папка TemporaryTracks не существует");
            }
        }

        private void ProjectCreateWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Если закрытие уже подтверждено, не вмешиваемся
            if (_isClosingConfirmed)
            {
                return;
            }

            // Отменяем закрытие окна, пока не обработаем логику
            e.Cancel = true;

            // Вызываем общий метод обработки закрытия
            HandleWindowClosing();
        }

        // Реализация DelegateCommand
        public class DelegateCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public DelegateCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

            public void Execute(object parameter) => _execute();
        }

        // Обработчик горячих клавиш
        private void ProjectCreateWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Проверяем, не находится ли фокус в TextBox (например, в поле темпа)
            if (Keyboard.FocusedElement is TextBox)
            {
                return; // Если фокус в TextBox, не обрабатываем горячие клавиши
            }

            // Пробел: воспроизведение или остановка записи
            if (e.Key == Key.Space)
            {
                if (isRecording)
                {
                    button2_Click(this, new RoutedEventArgs()); // Останавливаем запись
                }
                else
                {
                    play_btn_Click(this, new RoutedEventArgs()); // Начинаем воспроизведение
                }
                e.Handled = true; // Предотвращаем дальнейшую обработку пробела
            }

            // Ctrl+S: сохранение
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }

            // Delete: удаление выбранной дорожки
            if (e.Key == Key.Delete)
            {
                if (ListBox1.SelectedItem != null)
                {
                    DeleteTrackBtn_Click(this, new RoutedEventArgs());
                }
                else
                {
                    MessageBox.Show("Пожалуйста, выберите дорожку для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                e.Handled = true;
            }
        }

        private void ProjectCreateWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Обновляем исходную высоту, если панель закрыта
            if (mixerPanel.Visibility == Visibility.Collapsed)
            {
                originalRow2Height = MainGrid.RowDefinitions[1].ActualHeight;
            }
        }

        private void InitializeAudio()
        {
            try
            {
                // Проверяем доступность устройства записи
                if (WaveIn.DeviceCount == 0)
                {
                    throw new InvalidOperationException($"Нет доступных устройств записи. Устройств найдено: {WaveIn.DeviceCount}");
                }

                // Освобождаем предыдущие ресурсы
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                    System.Diagnostics.Debug.WriteLine("Предыдущий WaveIn освобождён");
                }
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                    writer = null;
                    System.Diagnostics.Debug.WriteLine("Предыдущий WaveFileWriter освобождён");
                }

                waveIn = new WaveIn();
                waveIn.DeviceNumber = SelectedInputDeviceIndex; // Используем выбранное устройство
                waveIn.WaveFormat = new WaveFormat(44100, 1);
                waveIn.BufferMilliseconds = 50;
                waveIn.DataAvailable += OnDataAvailable;
                writer = new WaveFileWriter(outputFilename, waveIn.WaveFormat);
                totalBytesRecorded = 0;

                waveformSamples.Clear();
                audioDataQueue = new ConcurrentQueue<float[]>();
                InitializeWaveformBitmap(canvas);
                waveIn.StartRecording();

                StartVisualizationProcessing();
                System.Diagnostics.Debug.WriteLine($"Аудиозапись инициализирована: WaveIn запущен, устройство: {WaveIn.GetCapabilities(waveIn.DeviceNumber).ProductName}, файл: {outputFilename}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка в InitializeAudio: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
                float[] samples = new float[e.BytesRecorded / 2];
                for (int i = 0; i < samples.Length; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    samples[i] = sample / 32768f;
                }
                audioDataQueue.Enqueue(samples);
                totalBytesRecorded += e.BytesRecorded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в OnDataAvailable: {ex.Message}");
            }
        }

        private void StartVisualizationProcessing()
        {
            Task.Run(async () =>
            {
                while (!visualizationCts.Token.IsCancellationRequested)
                {
                    if (audioDataQueue.TryDequeue(out float[] samples))
                    {
                        lock (waveformSamples)
                        {
                            waveformSamples.AddRange(samples);
                        }
                        await Application.Current.Dispatcher.InvokeAsync(UpdateWaveformBitmap, DispatcherPriority.Background);
                    }
                    await Task.Delay(20); // Быстрое обновление
                }
            }, visualizationCts.Token);
        }

        private void UpdateWaveformBitmap()
        {
            try
            {
                lock (waveformSamples)
                {
                    if (waveformSamples.Count == 0) return;

                    double sampleRate = 44100;
                    int samplesPerPixel = (int)Math.Max(1, sampleRate * 10 * pixelsPerSample / 44100);
                    int peakCount = waveformSamples.Count / samplesPerPixel;
                    double requiredWidth = peakCount * pixelsPerSample * compressionFactor;
                    requiredWidth = Math.Max(requiredWidth, 300);

                    // Обновляем размер битмапа, если нужно
                    if (requiredWidth > bitmapWidth)
                    {
                        bitmapWidth = (int)Math.Ceiling(requiredWidth);
                        var newBitmap = new WriteableBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Rgb24, null);
                        // Заполняем новый битмап белым фоном
                        byte[] newPixels = new byte[bitmapWidth * bitmapHeight * 3];
                        for (int i = 0; i < newPixels.Length; i += 3)
                        {
                            newPixels[i] = 255; // R
                            newPixels[i + 1] = 255; // G
                            newPixels[i + 2] = 255; // B
                        }
                        newBitmap.WritePixels(new Int32Rect(0, 0, bitmapWidth, bitmapHeight), newPixels, bitmapWidth * 3, 0);
                        // Копируем старое изображение
                        if (waveformBitmap != null)
                        {
                            byte[] oldPixels = new byte[waveformBitmap.PixelWidth * waveformBitmap.PixelHeight * 3];
                            waveformBitmap.CopyPixels(oldPixels, waveformBitmap.PixelWidth * 3, 0);
                            newBitmap.WritePixels(new Int32Rect(0, 0, waveformBitmap.PixelWidth, waveformBitmap.PixelHeight), oldPixels, waveformBitmap.PixelWidth * 3, 0);
                        }
                        var waveformImage = canvas.Children.OfType<Image>().FirstOrDefault();
                        if (waveformImage != null)
                        {
                            waveformImage.Source = newBitmap;
                        }
                        waveformBitmap = newBitmap;
                        canvas.Width = bitmapWidth;
                        ListBoxItem parentItem = FindParentListBoxItem(canvas);
                        if (parentItem != null)
                        {
                            parentItem.Width = bitmapWidth;
                        }
                    }


                    // Полное обновление для надежности
                    byte[] pixels = new byte[bitmapWidth * bitmapHeight * 3];
                    // Инициализируем белый фон
                    for (int i = 0; i < pixels.Length; i += 3)
                    {
                        pixels[i] = 255; // R
                        pixels[i + 1] = 255; // G
                        pixels[i + 2] = 255; // B
                    }

                    int centerY = bitmapHeight / 2;
                    float amplificationFactor = 2.0f;

                    for (int i = 0; i < peakCount; i++)
                    {
                        int startSample = i * samplesPerPixel;
                        int endSample = Math.Min(startSample + samplesPerPixel, waveformSamples.Count);

                        float max = float.MinValue;
                        float min = float.MaxValue;

                        for (int j = startSample; j < endSample; j++)
                        {
                            float sample = waveformSamples[j];
                            if (sample > max) max = sample;
                            if (sample < min) min = sample;
                        }

                        max = Math.Min(1.0f, Math.Max(-1.0f, max * amplificationFactor));
                        min = Math.Min(1.0f, Math.Max(-1.0f, min * amplificationFactor));

                        double x = i * pixelsPerSample * compressionFactor;
                        int pixelX = (int)x;
                        if (pixelX >= bitmapWidth) continue;

                        int maxY = centerY - (int)(max * (bitmapHeight / 2));
                        int minY = centerY - (int)(min * (bitmapHeight / 2));

                        for (int y = Math.Min(maxY, minY); y <= Math.Max(maxY, minY); y++)
                        {
                            if (y >= 0 && y < bitmapHeight)
                            {
                                int index = (y * bitmapWidth + pixelX) * 3;
                                pixels[index] = 0;     // R
                                pixels[index + 1] = 0; // G
                                pixels[index + 2] = 255; // B (синий)
                            }
                        }
                    }

                    waveformBitmap.WritePixels(new Int32Rect(0, 0, bitmapWidth, bitmapHeight), pixels, bitmapWidth * 3, 0);
                    Debug.WriteLine($"Waveform updated: {waveformSamples.Count} samples, {peakCount} peaks");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в UpdateWaveformBitmap: {ex.Message}");
            }
        }

        private ListBoxItem FindParentListBoxItem(DependencyObject child)
        {
            while (child != null && !(child is ListBoxItem))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as ListBoxItem;
        }

        private void DrawRecordedLine()
        {
            audioLine.Points = recordedPoints; // Устанавливаем сохраненные точки
            canvas.InvalidateVisual(); // Перерисовываем канвас, если требуется
        }

        // Метод останавливает запись и воспроизводит записанный файл с помощью WaveOutEvent
        private void play_btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (tracks.Count == 0 || tracks.All(t => t.Count == 0))
                {
                    MessageBox.Show("Нет записанных дорожек для воспроизведения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (waveOut == null || waveOut.PlaybackState == PlaybackState.Stopped)
                {
                    pause_btn.Opacity = 0.5;

                    waveOut?.Stop();
                    waveOut?.Dispose();
                    waveOut = null;

                    mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
                    mixer.ReadFully = true;

                    readers = new List<AudioFileReader>();
                    volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
                    maxTrackDuration = TimeSpan.Zero;

                    for (int i = 0; i < tracks.Count; i++)
                    {
                        if (tracks[i].Count > 0 && !mutedTracks[i])
                        {
                            var concatenatedProvider = ConcatenateTrack(tracks[i]);
                            var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
                            {
                                Volume = trackVolumes[i]
                            };
                            volumeProviders[i] = volumeProvider;
                            mixer.AddMixerInput(volumeProvider);
                            var trackDuration = GetTrackDuration(tracks[i]);
                            if (trackDuration > maxTrackDuration)
                            {
                                maxTrackDuration = trackDuration;
                            }
                        }
                    }

                    if (mixer.MixerInputs.Any())
                    {
                        masterVolumeProvider = new VolumeSampleProvider(mixer.ToWaveProvider().ToSampleProvider())
                        {
                            Volume = masterVolume
                        };
                        waveOut = new WaveOutEvent();
                        waveOut.Init(masterVolumeProvider);
                        waveOut.Play();
                        isLooping = false;

                        pause_btn.Opacity = 0.5;
                        refresh_btn.Opacity = 0.5;

                        if (isMetronomeOn)
                        {
                            MetronomeBtn_Click(sender, e);
                            MetronomeBtn_Click(sender, e);
                        }

                        // Запускаем анимацию
                        InitializeAnimation();
                        StartAnimation();

                        System.Diagnostics.Debug.WriteLine("Воспроизведение микса начато с начала");
                    }
                    else
                    {
                        MessageBox.Show("Все дорожки отключены или пусты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (waveOut.PlaybackState == PlaybackState.Paused)
                {
                    pause_btn.Opacity = 0.5;

                    waveOut.Play();
                    StartAnimation(); // Возобновляем анимацию
                    System.Diagnostics.Debug.WriteLine("Воспроизведение возобновлено с паузы");
                }
                else if (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;

                    mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
                    mixer.ReadFully = true;

                    readers = new List<AudioFileReader>();
                    volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
                    maxTrackDuration = TimeSpan.Zero;

                    for (int i = 0; i < tracks.Count; i++)
                    {
                        if (tracks[i].Count > 0 && !mutedTracks[i])
                        {
                            var concatenatedProvider = ConcatenateTrack(tracks[i]);
                            var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
                            {
                                Volume = trackVolumes[i]
                            };
                            volumeProviders[i] = volumeProvider;
                            mixer.AddMixerInput(volumeProvider);
                            var trackDuration = GetTrackDuration(tracks[i]);
                            if (trackDuration > maxTrackDuration)
                            {
                                maxTrackDuration = trackDuration;
                            }
                        }
                    }

                    if (mixer.MixerInputs.Any())
                    {
                        masterVolumeProvider = new VolumeSampleProvider(mixer.ToWaveProvider().ToSampleProvider())
                        {
                            Volume = masterVolume
                        };
                        waveOut = new WaveOutEvent();
                        waveOut.Init(masterVolumeProvider);
                        waveOut.Play();
                        isLooping = false;

                        pause_btn.Opacity = 0.5;
                        refresh_btn.Opacity = 0.5;

                        if (isMetronomeOn)
                        {
                            MetronomeBtn_Click(sender, e);
                            MetronomeBtn_Click(sender, e);
                        }

                        // Запускаем анимацию
                        InitializeAnimation();
                        StartAnimation();

                        System.Diagnostics.Debug.WriteLine("Воспроизведение микса перезапущено с начала");
                    }
                    else
                    {
                        MessageBox.Show("Все дорожки отключены или пусты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при воспроизведении микса: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка в Play: {ex.Message}");
            }
        }
        

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            playhead.Y2 = scrollViewer.ActualHeight;
        }

        private void InitializeAnimation()
        {
            // Вычисляем конечную позицию playhead (ширину самой длинной дорожки)
            double maxWidth = 200; // Начальная позиция (X1=200)
            if (maxTrackDuration != TimeSpan.Zero)
            {
                double sampleRate = 44100;
                double pixelsPerSample = 0.5; // Из VisualizeWaveform
                double compressionFactor = 0.1; // Из VisualizeWaveform
                double totalSamples = maxTrackDuration.TotalSeconds * sampleRate;
                maxWidth += (totalSamples / (sampleRate * 10 * pixelsPerSample / 44100)) * pixelsPerSample * compressionFactor;
                maxWidth = Math.Max(maxWidth, 300); // Минимальная ширина из VisualizeWaveform
            }

            animation = new DoubleAnimation
            {
                From = 200, // Начальная позиция (X1=200)
                To = maxWidth,
                Duration = new Duration(maxTrackDuration),
                FillBehavior = FillBehavior.Stop // Останавливается в конце
            };
            animation.Completed += Animation_Completed;
        }
        private void StartAnimation()
        {
            if (animation == null || animation.To == animation.From)
            {
                InitializeAnimation();
            }

            isPlaying = true;
            playhead.BeginAnimation(Canvas.LeftProperty, animation);
            playheadPolygon.BeginAnimation(Canvas.LeftProperty, animation);
            scrollTimer.Start();
            System.Diagnostics.Debug.WriteLine("Анимация playhead начата");
        }

        private void PauseAnimation()
        {
            isPlaying = false;
            playhead.BeginAnimation(Canvas.LeftProperty, null); // Останавливаем анимацию
            playheadPolygon.BeginAnimation(Canvas.LeftProperty, null);
            scrollTimer.Stop();
            System.Diagnostics.Debug.WriteLine("Анимация playhead приостановлена");
        }
        private void StopAnimation()
        {
            isPlaying = false;
            playhead.BeginAnimation(Canvas.LeftProperty, null); // Останавливаем анимацию
            playheadPolygon.BeginAnimation(Canvas.LeftProperty, null);
            playhead.SetValue(Canvas.LeftProperty, 200.0); // Сбрасываем в начальную позицию
            scrollTimer.Stop();
            scrollViewer.ScrollToHorizontalOffset(0);
            System.Diagnostics.Debug.WriteLine("Анимация playhead остановлена и сброшена");
        }
        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            double currentPosition = (double)playhead.GetValue(Canvas.LeftProperty);
            double scrollOffset = currentPosition - (scrollViewer.ViewportWidth / 2);
            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollOffset));
        }

        private void Animation_Completed(object sender, EventArgs e)
        {
            isPlaying = false;
            playhead.SetValue(Canvas.LeftProperty, (double)animation.To); // Остается в конце
            playheadPolygon.SetValue(Canvas.LeftProperty, (double)animation.To);
            scrollTimer.Stop();
            System.Diagnostics.Debug.WriteLine("Анимация playhead завершена");
        }

        // Метод для обновления позиции скроллинга во время анимации
        protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (isPlaying)
            {
                double currentPosition = playhead.GetValue(Canvas.LeftProperty) as double? ?? 0;
                // Центрирование линии в видимой области
                double scrollOffset = currentPosition - (scrollViewer.ViewportWidth / 2);
                scrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollOffset));
            }
        }

        /// <summary>
        /// Метод завершения записи
        /// </summary>
        private void StopRecording()
        {
            try
            {
                // Останавливаем запись
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                    System.Diagnostics.Debug.WriteLine("WaveIn остановлен и освобождён");
                }

                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                    writer = null;
                    System.Diagnostics.Debug.WriteLine("WaveFileWriter закрыт и освобождён");
                }

                // Останавливаем воспроизведение микса и метронома
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                    System.Diagnostics.Debug.WriteLine("WaveOut остановлен и освобождён");
                }

                // Очищаем readers
                if (readers != null)
                {
                    foreach (var reader in readers)
                    {
                        reader?.Dispose();
                    }
                    readers.Clear();
                    System.Diagnostics.Debug.WriteLine("Все AudioFileReaders освобождены");
                }

                // Останавливаем визуализацию
                visualizationCts.Cancel();
                visualizationCts.Dispose();
                visualizationCts = new CancellationTokenSource();

                // Визуализируем записанный файл
                if (File.Exists(outputFilename))
                {
                    VisualizeWaveform(outputFilename, canvas);
                }

                // Возобновляем метроном, если он был включён
                if (isMetronomeOn)
                {
                    MetronomeBtn_Click(this, new RoutedEventArgs());
                    MetronomeBtn_Click(this, new RoutedEventArgs());
                }

                System.Diagnostics.Debug.WriteLine("Запись и воспроизведение остановлены");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в StopRecording: {ex.Message}");
            }
        }

        /// <summary>
        /// Метод обработки нажатия кнопки начала записи
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ListBox1.SelectedIndex == -1)
                {
                    MessageBox.Show("Выберите дорожку для записи!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                // Останавливаем и очищаем предыдущие аудиоресурсы
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                    System.Diagnostics.Debug.WriteLine("Воспроизведение остановлено перед записью");
                }
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                }
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                    writer = null;
                }

                // Устанавливаем флаг записи
                isRecording = true;

                // Формируем уникальное имя файла для записи
                int selectedTrack = ListBox1.SelectedIndex;
                outputFilename = GenerateUniqueTrackFileName(selectedTrack);
                System.Diagnostics.Debug.WriteLine($"Создаётся файл записи: {outputFilename}");

                // Создаём новый Canvas для дорожки
                Canvas newCanvas = new Canvas
                {
                    Background = Brushes.White,
                    Height = 90,
                    Width = 10,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0),
                    Tag = outputFilename
                };

                // Добавляем Canvas в выбранную дорожку
                LB_Track_List[selectedTrack].Items.Add(newCanvas);
                canvas = newCanvas;
                trackCanvases[selectedTrack + 1] = newCanvas;

                // Убедимся, что коллекция tracks содержит достаточно дорожек
                while (tracks.Count <= selectedTrack)
                {
                    tracks.Add(new List<string>());
                    mutedTracks.Add(false);
                    trackVolumes[selectedTrack] = 1.0f;
                }

                // Инициализируем запись
                InitializeAudio();
                System.Diagnostics.Debug.WriteLine($"Запись начата для файла: {outputFilename}");

                // Инициализируем воспроизведение асинхронно
                (bool success, string errorMessage) = await InitializePlaybackDuringRecordingAsync();
                if (!success && errorMessage != "Нет активных дорожек или метронома для воспроизведения. Проверьте, включены ли дорожки.")
                {
                    MessageBox.Show($"Не удалось запустить воспроизведение: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Воспроизведение не запущено: {errorMessage}");
                    StopRecording();
                    return;
                }
                else if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("Воспроизведение не требуется (пустой проект или выключенные дорожки). Продолжаем запись.");
                }

                // Добавляем файл в tracks после начала записи
                tracks[selectedTrack].Add(outputFilename);

                // Логируем содержимое tracks
                System.Diagnostics.Debug.WriteLine("Содержимое tracks после начала записи:");
                for (int i = 0; i < tracks.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"Дорожка {i}: {string.Join(", ", tracks[i])}");
                }

                // Обновляем UI
                button1.Visibility = Visibility.Collapsed;
                button2.Visibility = Visibility.Visible;
                save_btn.Opacity = 1;

                System.Diagnostics.Debug.WriteLine($"Запись начата: {outputFilename}, воспроизведение {(success ? "запущено" : "не запущено")}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при начале записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка в button1_Click: {ex.Message}\nStackTrace: {ex.StackTrace}");
                StopRecording();
            }
        }

        private async Task<(bool Success, string ErrorMessage)> InitializePlaybackDuringRecordingAsync()
        {
            try
            {
                // Проверяем доступность устройства воспроизведения
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                if (!UseAsio && devices.Count == 0)
                {
                    string error = $"Нет доступных устройств воспроизведения. Устройств найдено: {devices.Count}";
                    System.Diagnostics.Debug.WriteLine(error);
                    return (false, error);
                }
                if (!UseAsio && (SelectedOutputDeviceIndex < 0 || SelectedOutputDeviceIndex >= devices.Count))
                {
                    SelectedOutputDeviceIndex = 0; // Сбрасываем на устройство по умолчанию
                    System.Diagnostics.Debug.WriteLine($"Недопустимый индекс устройства вывода: {SelectedOutputDeviceIndex}. Сброшено на 0.");
                }

                // Логируем доступные устройства
                if (!UseAsio)
                {
                    for (int i = 0; i < devices.Count; i++)
                    {
                        System.Diagnostics.Debug.WriteLine($"Устройство вывода {i}: {devices[i].FriendlyName}");
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(AsioDriverName))
                    {
                        string error = "ASIO-драйвер не выбран.";
                        System.Diagnostics.Debug.WriteLine(error);
                        return (false, error);
                    }
                    System.Diagnostics.Debug.WriteLine($"Используется ASIO драйвер: {AsioDriverName}");
                }

                // Очищаем предыдущие ресурсы
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }
                if (readers != null)
                {
                    foreach (var reader in readers)
                    {
                        reader?.Dispose();
                    }
                    readers.Clear();
                }

                // Создаём микшер
                var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
                mixer = new MixingSampleProvider(targetFormat);
                mixer.ReadFully = true;
                readers = new List<AudioFileReader>();
                volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
                maxTrackDuration = TimeSpan.Zero;

                System.Diagnostics.Debug.WriteLine($"Инициализация микшера: {tracks.Count} дорожек");

                // Очищаем несуществующие файлы из всех дорожек
                for (int i = 0; i < tracks.Count; i++)
                {
                    var originalFiles = tracks[i].ToList();
                    tracks[i] = tracks[i].Where(File.Exists).ToList();
                    if (originalFiles.Count != tracks[i].Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"Очищена дорожка {i}: {string.Join(", ", originalFiles)} -> {string.Join(", ", tracks[i])}");
                    }
                }

                // Добавляем активные дорожки
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].Count > 0 && !mutedTracks[i])
                    {
                        var concatenatedProvider = ConcatenateTrack(tracks[i]);
                        if (concatenatedProvider == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Пропущена дорожка {i}: нет валидных файлов.");
                            continue;
                        }
                        var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
                        {
                            Volume = trackVolumes.ContainsKey(i) ? trackVolumes[i] : 1.0f
                        };
                        volumeProviders[i] = volumeProvider;
                        mixer.AddMixerInput(volumeProvider);
                        var trackDuration = GetTrackDuration(tracks[i]);
                        if (trackDuration > maxTrackDuration)
                        {
                            maxTrackDuration = trackDuration;
                        }
                        System.Diagnostics.Debug.WriteLine($"Дорожка {i} добавлена: громкость {(trackVolumes.ContainsKey(i) ? trackVolumes[i] : 1.0f)}, длительность {trackDuration}");
                    }
                }

                // Добавляем метроном, если включён
                if (isMetronomeOn)
                {
                    TextBox tempoTextBox = FindName("TempoTextBox") as TextBox;
                    if (tempoTextBox == null || string.IsNullOrWhiteSpace(tempoTextBox.Text))
                    {
                        System.Diagnostics.Debug.WriteLine("TempoTextBox не найден или пуст. Метроном отключён.");
                    }
                    else if (!int.TryParse(tempoTextBox.Text, out int bpm) || bpm <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Некорректный темп метронома: {tempoTextBox.Text}. Метроном отключён.");
                    }
                    else
                    {
                        string metronomeFile = System.IO.Path.Combine("metronome", $"metronome{bpm}.wav");
                        if (!File.Exists(metronomeFile))
                        {
                            System.Diagnostics.Debug.WriteLine($"Файл метронома не найден: {metronomeFile}. Метроном отключён.");
                        }
                        else
                        {
                            try
                            {
                                var metronomeReader = new AudioFileReader(metronomeFile);
                                readers.Add(metronomeReader);
                                ISampleProvider metronomeProvider = metronomeReader.ToSampleProvider();
                                // Зацикливание через LoopingSampleProvider
                                metronomeProvider = new LoopingSampleProvider(metronomeProvider);
                                if (!metronomeReader.WaveFormat.Equals(targetFormat))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Преобразование формата метронома: {metronomeReader.WaveFormat} -> {targetFormat}");
                                    var waveProvider = new WaveFormatConversionProvider(targetFormat, metronomeProvider.ToWaveProvider());
                                    metronomeProvider = waveProvider.ToSampleProvider();
                                }
                                var volumeProvider = new VolumeSampleProvider(metronomeProvider)
                                {
                                    Volume = 1.0f // Регулируемая громкость метронома
                                };
                                mixer.AddMixerInput(volumeProvider);
                                System.Diagnostics.Debug.WriteLine($"Метроном добавлен: BPM {bpm}, файл: {metronomeFile}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при добавлении метронома: {ex.Message}. Метроном отключён.");
                            }
                        }
                    }
                }

                // Инициализируем воспроизведение, если есть потоки, или продолжаем без воспроизведения
                try
                {
                    string deviceName;
                    if (UseAsio && !string.IsNullOrEmpty(AsioDriverName))
                    {
                        waveOut = new AsioOut(AsioDriverName);
                        deviceName = AsioDriverName;
                    }
                    else
                    {
                        var selectedDevice = devices[SelectedOutputDeviceIndex];
                        waveOut = new WasapiOut(selectedDevice, AudioClientShareMode.Shared, true, 50);
                        deviceName = selectedDevice.FriendlyName;
                    }

                    if (mixer.MixerInputs.Any())
                    {
                        masterVolumeProvider = new VolumeSampleProvider(mixer)
                        {
                            Volume = masterVolume
                        };
                        waveOut.Init(masterVolumeProvider.ToWaveProvider());
                        await Task.Run(() => waveOut.Play());
                        System.Diagnostics.Debug.WriteLine($"Воспроизведение начато: {mixer.MixerInputs.Count()} потоков, устройство: {deviceName}, мастер-громкость: {masterVolume}");
                        return (true, null);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Нет активных дорожек или метронома. Воспроизведение не запущено, но запись возможна.");
                        return (true, "Воспроизведение не запущено, но запись возможна.");
                    }
                }
                catch (Exception ex)
                {
                    string error = $"Ошибка инициализации устройства вывода: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine(error);
                    return (false, error);
                }
            }
            catch (Exception ex)
            {
                string error = $"Ошибка при инициализации воспроизведения: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Ошибка в InitializePlaybackDuringRecordingAsync: {ex.Message}\nStackTrace: {ex.StackTrace}");
                // Очищаем ресурсы
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }
                if (readers != null)
                {
                    foreach (var reader in readers)
                    {
                        reader?.Dispose();
                    }
                    readers.Clear();
                }
                return (false, error);
            }
        }

        private string GenerateUniqueTrackFileName(int trackIndex)
        {
            string baseFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects", "TemporaryTracks");
            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
                System.Diagnostics.Debug.WriteLine($"Создана папка: {baseFolder}");
            }

            List<string> existingFiles = tracks.Count > trackIndex ? tracks[trackIndex].Select(System.IO.Path.GetFileNameWithoutExtension).ToList() : new List<string>();
            int index = 1;
            string fileName;

            while (true)
            {
                fileName = $"track{trackIndex + 1}-{index}.wav";
                if (!existingFiles.Contains($"track{trackIndex + 1}-{index}") && !File.Exists(System.IO.Path.Combine(baseFolder, fileName)))
                {
                    break;
                }
                index++;
            }

            string fullPath = System.IO.Path.Combine(baseFolder, fileName);
            System.Diagnostics.Debug.WriteLine($"Сгенерировано уникальное имя файла: {fullPath}");
            return fullPath;
        }

        private ISampleProvider ConcatenateTrack(List<string> trackFiles)
        {
            if (trackFiles == null || !trackFiles.Any())
            {
                System.Diagnostics.Debug.WriteLine("Дорожка пуста или не содержит файлов.");
                return null;
            }

            // Очищаем несуществующие файлы
            var validFiles = trackFiles.Where(File.Exists).ToList();
            if (!validFiles.Any())
            {
                System.Diagnostics.Debug.WriteLine("Все файлы дорожки не найдены.");
                return null;
            }

            try
            {
                var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
                var providers = validFiles.Select(file =>
                {
                    System.Diagnostics.Debug.WriteLine($"Загрузка файла: {file}");
                    var reader = new AudioFileReader(file);
                    readers.Add(reader);
                    // Преобразуем формат, если необходимо
                    if (!reader.WaveFormat.Equals(targetFormat))
                    {
                        System.Diagnostics.Debug.WriteLine($"Преобразование формата для {file}: {reader.WaveFormat} -> {targetFormat}");
                        var waveProvider = new WaveFormatConversionProvider(targetFormat, reader.ToWaveProvider());
                        return waveProvider.ToSampleProvider();
                    }
                    return reader.ToSampleProvider();
                }).ToList();

                var concatenated = new ConcatenatingSampleProvider(providers);
                System.Diagnostics.Debug.WriteLine($"Дорожка с {validFiles.Count} файлами успешно сконкатенирована.");
                return concatenated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при конкатенации дорожки: {ex.Message}");
                return null;
            }
        }

        private TimeSpan GetTrackDuration(List<string> trackFiles)
        {
            TimeSpan totalDuration = TimeSpan.Zero;
            foreach (var file in trackFiles)
            {
                using (var reader = new AudioFileReader(file))
                {
                    totalDuration += reader.TotalTime;
                }
            }
            return totalDuration;
        }

        // Метод обработки нажатия кнопки завершения записи
        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (waveIn != null)
            {
                isRecording = false;

                button1.Visibility = Visibility.Visible;
                button2.Visibility = Visibility.Collapsed;
                save_btn.Opacity = 1;

                spectrum.Stop(); // Остановить анализ

                StopRecording();
            }
        }

        // Добавление дорожки
        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            ListBox1AddItems();
            save_btn.Opacity = 1;
        }

        private void ListBox1AddItems()
        {
            // Окно с выбором инструмента
            InstrumentsSelectWindow instrumentsSelectWindow = new InstrumentsSelectWindow(project);
            instrumentsSelectWindow.ShowDialog();

            if (instrumentsSelectWindow.instrumentName != "" && instrumentsSelectWindow.instrumentImagePath != "")
            {
                ListBoxItem item1 = new ListBoxItem();
                item1.Padding = new Thickness(0);
                StackPanel stackPanel1 = new StackPanel();
                stackPanel1.Orientation = Orientation.Horizontal;
                stackPanel1.Height = 100;

                Grid grid1 = new Grid();
                grid1.Width = 190; // 170
                grid1.Background = new SolidColorBrush(Colors.LightSkyBlue);
                grid1.MouseRightButtonDown += TrackGrid_MouseRightButtonDown;

                // Создаем контекстное меню
                ContextMenu contextMenu = new ContextMenu();
                if (instrumentsSelectWindow.instrumentName != "Recording")
                {
                    // Создаем пункт меню "Открыть инструмент"
                    MenuItem openInstrumentItem = new MenuItem();
                    openInstrumentItem.Header = "Открыть инструмент";
                    openInstrumentItem.Click += (sender, e) => OpenInstrument(sender, e, instrumentsSelectWindow.instrumentName);
                    // Добавляем пункт в контекстное меню
                    contextMenu.Items.Add(openInstrumentItem);
                }
                // Привязываем контекстное меню к элементу Grid
                grid1.ContextMenu = contextMenu;

                // Ползунок громкости
                Slider volumeSlider = new Slider
                {
                    Height = 90,
                    Value = 1, // По умолчанию максимальная громкость
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    Tag = tracks.Count // Индекс дорожки
                };
                volumeSlider.ValueChanged += VolumeSlider_ValueChanged;

                TextBlock tb1 = new TextBlock();
                tb1.Text = instrumentsSelectWindow.instrumentName;
                tb1.Margin = new Thickness(100, 20, 5, 0);

                Image image1 = new Image();
                image1.HorizontalAlignment = HorizontalAlignment.Left;
                image1.VerticalAlignment = VerticalAlignment.Center;
                image1.Width = 70;
                image1.Height = 70;
                image1.Margin = new Thickness(25, 10, 0, 10);
                image1.Source = new BitmapImage(new Uri(instrumentsSelectWindow.instrumentImagePath));

                TextBox tb2 = new TextBox();
                string trackName = $"Track{TrackNames.Count + 1}";
                TrackNames.Add(trackName);
                tb2.Text = trackName;
                tb2.Margin = new Thickness(100, 40, 5, 40);
                tb2.TextChanged += Tb2_TextChanged;
                tb2.KeyDown += TextBox_KeyDown;

                // Кнопки действий с дорожкой
                // Удалить
                Button delete_btn = new Button();
                Image image_del = new Image();
                image_del.Source = new BitmapImage(new Uri("pack://application:,,,/Images/icon-dustbin.png"));
                delete_btn.Content = image_del;
                delete_btn.Width = 25;
                delete_btn.HorizontalAlignment = HorizontalAlignment.Left;
                delete_btn.VerticalAlignment = VerticalAlignment.Bottom;
                delete_btn.Margin = new Thickness(100, 0, 0, 10);
                delete_btn.Background = null;
                delete_btn.BorderBrush = null;
                delete_btn.ToolTip = "Удалить трек";
                delete_btn.Click += DeleteTrackBtn_Click;

                // Кнопка "Mute"
                Button mute_btn = new Button();
                Image image_mute = new Image();
                image_mute.Source = new BitmapImage(new Uri("pack://application:,,,/Images/icon-unmute.png"));
                mute_btn.Content = image_mute;
                mute_btn.Width = 25;
                mute_btn.HorizontalAlignment = HorizontalAlignment.Left;
                mute_btn.VerticalAlignment = VerticalAlignment.Bottom;
                mute_btn.Margin = new Thickness(130, 0, 0, 10);
                mute_btn.Padding = new Thickness(-2);
                mute_btn.Background = null;
                mute_btn.BorderBrush = null;
                mute_btn.ToolTip = "Выключить/включить трек";
                mute_btn.Click += MuteTrackBtn_Click;
                mute_btn.Tag = tracks.Count; // Привязываем индекс дорожки

                grid1.Children.Add(volumeSlider);
                grid1.Children.Add(tb1);
                grid1.Children.Add(image1);
                grid1.Children.Add(tb2);
                grid1.Children.Add(delete_btn);
                grid1.Children.Add(mute_btn);
                stackPanel1.Children.Add(grid1);

                ListBox LB_Track1 = new ListBox();
                LB_Track1.Background = null;
                LB_Track1.Padding = new Thickness(0);
                // Создаем ItemsPanelTemplate
                ItemsPanelTemplate itemsPanelTemplate = new ItemsPanelTemplate();
                var stackPanelFactory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
                // Устанавливаем Orientation для VirtualizingStackPanel
                stackPanelFactory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Horizontal);
                // Устанавливаем VisualTree для ItemsPanelTemplate
                itemsPanelTemplate.VisualTree = stackPanelFactory;
                // Устанавливаем ItemsPanel
                LB_Track1.ItemsPanel = itemsPanelTemplate;
                LB_Track_List.Add(LB_Track1);
                stackPanel1.Children.Add(LB_Track1);

                item1.Content = stackPanel1;
                items.Add(item1);
                tracks.Add(new List<string>()); // Добавляем новую дорожку
                mutedTracks.Add(false); // По умолчанию дорожка включена
                trackVolumes[tracks.Count - 1] = 1.0f; // Начальная громкость 100%
                volumeProviders.Add(null); // Пустой провайдер, заполнится при воспроизведении
                for (int i = 0; i < BeatsLineList.Count; i++)
                {
                    UpdateLineHeight(BeatsLineList[i]);
                }
            }
        }

        private void TrackGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var grid = (Grid)sender;
            grid.ContextMenu.IsOpen = true;
        }

        // Метод, вызываемый при выборе пункта меню
        private void OpenInstrument(object sender, RoutedEventArgs e, string instrument)
        {
            if (instrument == "Synthesizer")
            {
                PlaySynthesizerWindow playSynthesizerWindow = new PlaySynthesizerWindow(project);
                playSynthesizerWindow.Show();
            }
        }

        private void Tb2_TextChanged(object sender, TextChangedEventArgs e)
        {
            save_btn.Opacity = 1;
        }

        // Метод обновляет громкость дорожки в реальном времени
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            save_btn.Opacity = 1;

            var slider = (Slider)sender;
            int trackIndex = (int)slider.Tag;
            float newVolume = (float)e.NewValue;

            trackVolumes[trackIndex] = newVolume;
            System.Diagnostics.Debug.WriteLine($"Громкость дорожки {trackIndex + 1} изменена на {newVolume}");

            // Обновляем громкость в реальном времени, если воспроизведение активно
            if (waveOut != null && volumeProviders[trackIndex] != null && (waveOut.PlaybackState == PlaybackState.Playing || waveOut.PlaybackState == PlaybackState.Paused))
            {
                volumeProviders[trackIndex].Volume = newVolume;
            }
        }

        // Кнопка "Удалить" - метод удаления дорожки (элемента ListBox1)
        private void DeleteTrackBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("Уверены, что хотите удалить трек?", "Подтвердите удаление", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                save_btn.Opacity = 1;

                var button = (Button)sender;
                var li = FindAncestor<ListBoxItem>(button);
                var lb = GetParentListBox(button);
                var o = lb.ItemContainerGenerator.ItemFromContainer(li);

                int trackIndex = items.IndexOf(li);

                // Логируем имя удаляемой дорожки
                System.Diagnostics.Debug.WriteLine($"Удаляем дорожку {trackIndex + 1}: TrackNames[{trackIndex}]={TrackNames[trackIndex]}");

                // Останавливаем запись
                if (waveIn != null)
                {
                    StopRecording();
                }

                // Освобождаем все ресурсы воспроизведения
                try
                {
                    if (waveOut != null)
                    {
                        waveOut.Stop();
                        waveOut.Dispose();
                        waveOut = null;
                        System.Diagnostics.Debug.WriteLine("waveOut остановлен и освобождён");
                    }

                    if (readers != null)
                    {
                        foreach (var reader in readers)
                        {
                            try
                            {
                                reader?.Dispose();
                                System.Diagnostics.Debug.WriteLine("AudioFileReader освобождён");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении AudioFileReader: {ex.Message}");
                            }
                        }
                        readers.Clear();
                    }

                    if (mixer != null)
                    {
                        mixer.RemoveAllMixerInputs();
                        mixer = null;
                        System.Diagnostics.Debug.WriteLine("mixer очищен");
                    }

                    volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
                    masterVolumeProvider = null;
                    System.Diagnostics.Debug.WriteLine("volumeProviders и masterVolumeProvider сброшены");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении аудиоресурсов: {ex.Message}");
                }

                // Удаляем файлы, связанные с дорожкой, из tracks[trackIndex]
                foreach (var fileName in tracks[trackIndex].ToList()) // ToList, чтобы избежать модификации коллекции
                {
                    const int maxAttempts = 3;
                    const int delayMs = 100;
                    bool deleted = false;

                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            if (File.Exists(fileName))
                            {
                                File.Delete(fileName);
                                System.Diagnostics.Debug.WriteLine($"Файл удалён: {fileName} (попытка {attempt})");
                                deleted = true;
                                break;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Файл не найден: {fileName}");
                                deleted = true;
                                break;
                            }
                        }
                        catch (IOException ex) when (ex.Message.Contains("используется другим процессом"))
                        {
                            System.Diagnostics.Debug.WriteLine($"Файл {fileName} заблокирован (попытка {attempt}): {ex.Message}");
                            if (attempt < maxAttempts)
                            {
                                Thread.Sleep(delayMs);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при удалении файла {fileName} (попытка {attempt}): {ex.Message}");
                            MessageBox.Show($"Ошибка при удалении файла {fileName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    }

                    if (deleted)
                    {
                        // Удаляем из trackFiles
                        if (trackFiles.Contains(fileName))
                        {
                            trackFiles.Remove(fileName);
                        }

                        // Удаляем из trackWaveforms
                        if (trackWaveforms.ContainsKey(fileName))
                        {
                            trackWaveforms.Remove(fileName);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Не удалось удалить файл {fileName} после {maxAttempts} попыток");
                        MessageBox.Show($"Не удалось удалить файл {fileName}: файл всё ещё используется.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // Сохраняем имя дорожки перед удалением
                string trackNameToDelete = TrackNames[trackIndex];

                // Удаляем дорожку из списков
                tracks.RemoveAt(trackIndex);
                mutedTracks.RemoveAt(trackIndex);
                trackVolumes.Remove(trackIndex);
                volumeProviders.RemoveAt(trackIndex);
                LB_Track_List.RemoveAt(trackIndex);
                TrackNames.RemoveAt(trackIndex);

                // Обновляем индексы в trackVolumes
                var newTrackVolumes = new Dictionary<int, float>();
                foreach (var kvp in trackVolumes)
                {
                    if (kvp.Key > trackIndex)
                    {
                        newTrackVolumes[kvp.Key - 1] = kvp.Value;
                    }
                    else
                    {
                        newTrackVolumes[kvp.Key] = kvp.Value;
                    }
                }
                trackVolumes = newTrackVolumes;

                // Обновляем volumeProviders
                for (int i = 0; i < volumeProviders.Count; i++)
                {
                    if (i >= trackIndex)
                    {
                        volumeProviders[i] = i + 1 < volumeProviders.Count ? volumeProviders[i + 1] : null;
                    }
                }
                if (volumeProviders.Count > tracks.Count)
                {
                    volumeProviders.RemoveAt(volumeProviders.Count - 1);
                }

                // Проверяем, существует ли проект (задано ли projectName и есть ли файл .daw)
                if (!string.IsNullOrEmpty(projectName))
                {
                    string basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
                    string projectFolder = System.IO.Path.Combine(basePath, projectName);
                    string favoritesFolder = System.IO.Path.Combine(basePath, "Favorites", projectName);
                    string dawFileName = $"{projectName}.daw";
                    string dawFilePath = System.IO.Path.Combine(projectFolder, dawFileName);
                    string dawFavoritesPath = System.IO.Path.Combine(favoritesFolder, dawFileName);

                    if (File.Exists(dawFilePath) || File.Exists(dawFavoritesPath))
                    {
                        // Обновляем файл .daw, если проект существует
                        try
                        {
                            UpdateDawFile(trackNameToDelete);
                            System.Diagnostics.Debug.WriteLine($"Файл .daw обновлён, удалена строка для {trackNameToDelete}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении файла .daw: {ex.Message}");
                            MessageBox.Show($"Ошибка при обновлении файла проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Файл .daw не существует для проекта {projectName}: проверены {dawFilePath} и {dawFavoritesPath}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Пропущено обновление .daw: projectName не задано (несохранённый проект)");
                }

                // Удаляем визуальную часть
                items.Remove(li);
                System.Diagnostics.Debug.WriteLine($"Дорожка {trackIndex + 1} удалена из ListBox");
            }
        }

        private void UpdateDawFile(string trackName)
        {
            // Базовый путь
            string basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            string projectFolder = System.IO.Path.Combine(basePath, projectName);
            string favoritesFolder = System.IO.Path.Combine(basePath, "Favorites", projectName);
            string dawFileName = $"{projectName}.daw";
            string dawFilePath = System.IO.Path.Combine(projectFolder, dawFileName);
            string dawFavoritesPath = System.IO.Path.Combine(favoritesFolder, dawFileName);

            // Логируем пути и trackName
            System.Diagnostics.Debug.WriteLine($"projectName: {projectName}, trackName: {trackName}");
            System.Diagnostics.Debug.WriteLine($"Проверка пути: {dawFilePath}");
            System.Diagnostics.Debug.WriteLine($"Проверка пути Favorites: {dawFavoritesPath}");

            // Проверяем, где находится файл .daw
            string actualDawFilePath = null;
            if (File.Exists(dawFilePath))
            {
                actualDawFilePath = dawFilePath;
                System.Diagnostics.Debug.WriteLine($"Файл .daw найден в: {dawFilePath}");
            }
            else if (File.Exists(dawFavoritesPath))
            {
                actualDawFilePath = dawFavoritesPath;
                System.Diagnostics.Debug.WriteLine($"Файл .daw найден в: {dawFavoritesPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Файл .daw не найден ни в {dawFilePath}, ни в {dawFavoritesPath}");
                return;
            }

            // Читаем все строки файла
            List<string> lines;
            try
            {
                lines = File.ReadAllLines(actualDawFilePath).ToList();
                System.Diagnostics.Debug.WriteLine($"Прочитано {lines.Count} строк из {actualDawFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения файла .daw: {ex.Message}");
                throw;
            }

            // Формируем новое содержимое, исключая строку с trackName
            List<string> updatedLines = new List<string>();
            bool trackFound = false;

            for (int i = 0; i < lines.Count; i++)
            {
                // Сохраняем первую строку (mixDuration,bpm,mixVolume)
                if (i == 0)
                {
                    updatedLines.Add(lines[i]);
                    continue;
                }

                // Проверяем, начинается ли строка с trackName
                if (lines[i].StartsWith($"{trackName},"))
                {
                    trackFound = true;
                    System.Diagnostics.Debug.WriteLine($"Найдена и пропущена строка: {lines[i]}");
                    continue;
                }

                // Сохраняем остальные строки
                updatedLines.Add(lines[i]);
            }

            // Проверяем, была ли найдена строка
            if (!trackFound)
            {
                System.Diagnostics.Debug.WriteLine($"Строка для {trackName} не найдена в {actualDawFilePath}");
            }

            // Логируем содержимое перед записью
            System.Diagnostics.Debug.WriteLine($"Содержимое .daw перед записью:\n{string.Join("\n", updatedLines)}");

            // Записываем обновлённое содержимое
            try
            {
                File.WriteAllLines(actualDawFilePath, updatedLines);
                System.Diagnostics.Debug.WriteLine($"Файл .daw успешно записан: {actualDawFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка записи файла .daw: {ex.Message}");
                throw;
            }
        }

        private ListBox GetParentListBox(DependencyObject child)
        {
            while (child != null)
            {
                if (child is ListBox listBox)
                    return listBox;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        /// <summary>
        /// Метод обрабатывает включение/отключение трека
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MuteTrackBtn_Click(object sender, RoutedEventArgs e)
        {
            save_btn.Opacity = 1;

            var button = (Button)sender;
            int trackIndex = (int)button.Tag;

            mutedTracks[trackIndex] = !mutedTracks[trackIndex]; // Переключаем состояние
            Image muteImage = (Image)button.Content;

            if (mutedTracks[trackIndex])
            {
                muteImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/icon-mute.png")); // Иконка для "включить"
                System.Diagnostics.Debug.WriteLine($"Дорожка {trackIndex + 1} отключена");
            }
            else
            {
                muteImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/icon-unmute.png")); // Иконка для "отключить"
                System.Diagnostics.Debug.WriteLine($"Дорожка {trackIndex + 1} включена");
            }
        }

        private void DrawGridLines()
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Margin = new Thickness(step, 0, 0, 0);
            TextBlock textBlock = new TextBlock();
            textBlock.Text = "Такт 1";
            for (int i = 0; i < BeatsLineList.Count; i++)
            {
                textBlock.Text = $"Такт {i + 2}";
            }
            textBlock.FontSize = 16;
            textBlock.Margin = new Thickness(-20, 0, 0, 0);
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(83, 93, 110));

            Line line = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = scrollViewer.ActualHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(130, 139, 153)),
                StrokeThickness = 1
            };
            UpdateLineHeight(line);
            BeatsLineList.Add(line);

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(line);
            Beats_DockPanel.Children.Add(stackPanel);
        }

        public void UpdateLineHeight(Line line)
        {
            Binding binding = new Binding();
            binding.ElementName = "scrollViewer";
            binding.Path = new PropertyPath("ActualHeight");
            Converter bindingConverter = new Converter();
            binding.Converter = bindingConverter;
            line.SetBinding(Line.Y2Property, binding);
        }

        public class Converter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is double height)
                {
                    return height - 20; // вычитание 20 пикселей
                }
                return 0;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            }
            else
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            }
            e.Handled = true;
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void MetronomeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isMetronomeOn)
            {
                // Не включаем метроном, если идёт запись
                if (isRecording)
                {
                    MessageBox.Show("Нельзя включить метроном во время записи!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                try
                {
                    TextBox tempoTextBox = FindName("TempoTextBox") as TextBox;
                    if (tempoTextBox == null || string.IsNullOrWhiteSpace(tempoTextBox.Text))
                    {
                        MessageBox.Show("Введите корректный темп в поле TempoTextBox!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    int bpm = int.Parse(tempoTextBox.Text);
                    if (bpm <= 0 || bpm > 300)
                    {
                        MessageBox.Show("Темп должен быть в диапазоне от 1 до 300 BPM!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }

                    string metronomeFile = $"metronome/metronome{bpm}.wav";
                    if (File.Exists(metronomeFile))
                    {
                        metronome_btn.Opacity = 1;

                        metronomeOut?.Dispose();
                        metronomeOut = new WaveOutEvent();
                        var metronomeReader = new AudioFileReader(metronomeFile);
                        var loopStream = new LoopStream(metronomeReader);
                        metronomeOut.Init(loopStream);
                        metronomeOut.Play();
                        isMetronomeOn = true;
                        System.Diagnostics.Debug.WriteLine($"Метроном включен, BPM: {bpm}");
                    }
                    else
                    {
                        MessageBox.Show("Ошибка: файл метронома не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при запуске метронома: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    metronomeOut?.Dispose();
                    metronomeOut = null;
                    isMetronomeOn = false;
                }
            }
            else
            {
                try
                {
                    metronome_btn.Opacity = 0.5;

                    metronomeOut?.Stop();
                    metronomeOut?.Dispose();
                    metronomeOut = null;
                    isMetronomeOn = false;
                    System.Diagnostics.Debug.WriteLine("Метроном выключен");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при выключении метронома: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Класс для зацикливания метронома
        public class LoopStream : WaveStream
        {
            private readonly WaveStream sourceStream;

            public LoopStream(WaveStream sourceStream)
            {
                this.sourceStream = sourceStream;
            }

            public override WaveFormat WaveFormat => sourceStream.WaveFormat;

            public override long Length => sourceStream.Length;

            public override long Position
            {
                get => sourceStream.Position;
                set => sourceStream.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalBytesRead = 0;
                while (totalBytesRead < count)
                {
                    int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        sourceStream.Position = 0; // Перематываем в начало
                        bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    }
                    totalBytesRead += bytesRead;
                }
                return totalBytesRead;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    sourceStream.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Создаём базовую папку Projects, если её нет
            string projectsBaseFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            if (!Directory.Exists(projectsBaseFolder))
            {
                Directory.CreateDirectory(projectsBaseFolder);
            }

            // Если проект уже сохранён, обновляем существующий
            if (isProjectSaved)
            {
                UpdateProject();
                save_btn.Opacity = 0.5;
                return;
            }

            // Предлагаем имя проекта по умолчанию
            string defaultProjectName = "Project1";
            int projectIndex = 1;
            string favoritesFolder = System.IO.Path.Combine(projectsBaseFolder, "Favorites");
            while (Directory.Exists(System.IO.Path.Combine(projectsBaseFolder, defaultProjectName)) ||
                   Directory.Exists(System.IO.Path.Combine(favoritesFolder, defaultProjectName)))
            {
                projectIndex++;
                defaultProjectName = $"Project{projectIndex}";
            }

            // Создаём окно для ввода имени проекта
            var inputDialog = new Window
            {
                Title = "Сохранение проекта",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBlock = new TextBlock { Text = "Введите название проекта:" };
            var textBox = new TextBox { Text = defaultProjectName, Margin = new Thickness(0, 5, 0, 10) };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Отмена", Width = 75 };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);
            inputDialog.Content = stackPanel;

            bool? dialogResult = null;
            okButton.Click += (s, ev) =>
            {
                dialogResult = true;
                inputDialog.Close();
            };
            cancelButton.Click += (s, ev) =>
            {
                dialogResult = false;
                inputDialog.Close();
            };

            inputDialog.ShowDialog();

            if (dialogResult != true)
            {
                return; // Пользователь отменил сохранение
            }

            // Получаем имя проекта
            projectName = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(projectName))
            {
                MessageBox.Show("Имя проекта не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем, не совпадает ли имя с "TemporaryTracks" или "Favorites"
            if (projectName.Equals("TemporaryTracks", StringComparison.OrdinalIgnoreCase) ||
                projectName.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Имя проекта не может быть 'TemporaryTracks' или 'Favorites'!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Проверяем существование имени проекта в папках Projects и Favorites
            string newProjectFolderInProjects = System.IO.Path.Combine(projectsBaseFolder, projectName);
            string newProjectFolderInFavorites = System.IO.Path.Combine(favoritesFolder, projectName);
            if (Directory.Exists(newProjectFolderInProjects) || Directory.Exists(newProjectFolderInFavorites))
            {
                MessageBox.Show("Проект с таким именем уже существует в папке Projects или Favorites!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Попытка создать проект с именем {projectName}, но он уже существует в {newProjectFolderInProjects} или {newProjectFolderInFavorites}");
                return;
            }

            // Создаём папку проекта
            projectFolder = System.IO.Path.Combine(projectsBaseFolder, projectName);
            if (!Directory.Exists(projectFolder))
            {
                Directory.CreateDirectory(projectFolder);
                System.Diagnostics.Debug.WriteLine($"Создана папка проекта: {projectFolder}");
            }

            // Создаём подпапку Tracks
            string projectTracksFolder = System.IO.Path.Combine(projectFolder, "Tracks");
            if (!Directory.Exists(projectTracksFolder))
            {
                Directory.CreateDirectory(projectTracksFolder);
                System.Diagnostics.Debug.WriteLine($"Создана папка Tracks: {projectTracksFolder}");
            }

            // Копируем все треки из TemporaryTracks в новую папку
            foreach (var trackListBox in LB_Track_List)
            {
                foreach (Canvas canvas in trackListBox.Items)
                {
                    if (canvas.Tag != null && File.Exists(canvas.Tag.ToString()))
                    {
                        string oldFilePath = canvas.Tag.ToString();
                        string fileName = System.IO.Path.GetFileName(oldFilePath);
                        string newFilePath = System.IO.Path.Combine(projectTracksFolder, fileName);

                        try
                        {
                            File.Copy(oldFilePath, newFilePath, true);
                            System.Diagnostics.Debug.WriteLine($"Скопирован файл: {oldFilePath} -> {newFilePath}");

                            // Обновляем путь в tracks
                            for (int i = 0; i < tracks.Count; i++)
                            {
                                for (int j = 0; j < tracks[i].Count; j++)
                                {
                                    if (tracks[i][j] == oldFilePath)
                                    {
                                        tracks[i][j] = newFilePath;
                                    }
                                }
                            }

                            // Обновляем Tag у Canvas
                            canvas.Tag = newFilePath;

                            // Обновляем trackWaveforms
                            if (trackWaveforms.ContainsKey(oldFilePath))
                            {
                                var polyline = trackWaveforms[oldFilePath];
                                trackWaveforms.Remove(oldFilePath);
                                trackWaveforms[newFilePath] = polyline;
                            }

                            // Обновляем trackFiles
                            if (trackFiles.Contains(oldFilePath))
                            {
                                trackFiles.Remove(oldFilePath);
                                trackFiles.Add(newFilePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при копировании файла {fileName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            System.Diagnostics.Debug.WriteLine($"Ошибка при копировании файла {fileName}: {ex.Message}");
                            return;
                        }
                    }
                }
            }

            // Сохраняем файл проекта
            SaveProjectFile();

            // Устанавливаем флаг, что проект сохранён
            isProjectSaved = true;
            save_btn.Opacity = 0.5;
            System.Diagnostics.Debug.WriteLine($"Проект успешно сохранён: {projectFolder}");
        }

        private void UpdateProject()
        {
            // Создаём подпапку Tracks, если её нет
            string projectTracksFolder = System.IO.Path.Combine(projectFolder, "Tracks");
            if (!Directory.Exists(projectTracksFolder))
            {
                Directory.CreateDirectory(projectTracksFolder);
            }

            // Копируем новые треки из TemporaryTracks в папку проекта
            foreach (var trackListBox in LB_Track_List)
            {
                foreach (Canvas canvas in trackListBox.Items)
                {
                    if (canvas.Tag != null && File.Exists(canvas.Tag.ToString()))
                    {
                        string filePath = canvas.Tag.ToString();
                        if (filePath.StartsWith(temporaryTracksFolder))
                        {
                            string fileName = System.IO.Path.GetFileName(filePath);
                            string newFilePath = System.IO.Path.Combine(projectTracksFolder, fileName);

                            try
                            {
                                File.Copy(filePath, newFilePath, true);
                                System.Diagnostics.Debug.WriteLine($"Скопирован файл: {filePath} -> {newFilePath}");

                                // Обновляем путь в tracks
                                for (int i = 0; i < tracks.Count; i++)
                                {
                                    for (int j = 0; j < tracks[i].Count; j++)
                                    {
                                        if (tracks[i][j] == filePath)
                                        {
                                            tracks[i][j] = newFilePath;
                                        }
                                    }
                                }

                                // Обновляем Tag у Canvas
                                canvas.Tag = newFilePath;

                                // Обновляем trackWaveforms
                                if (trackWaveforms.ContainsKey(filePath))
                                {
                                    var polyline = trackWaveforms[filePath];
                                    trackWaveforms.Remove(filePath);
                                    trackWaveforms[newFilePath] = polyline;
                                }

                                // Обновляем trackFiles
                                if (trackFiles.Contains(filePath))
                                {
                                    trackFiles.Remove(filePath);
                                    trackFiles.Add(newFilePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Ошибка при копировании файла {fileName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                }
            }

            // Сохраняем обновлённый файл проекта
            SaveProjectFile();
        }

        private void SaveProjectFile()
        {
            // Вычисляем максимальную длительность микса
            maxTrackDuration = TimeSpan.Zero;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Count > 0)
                {
                    var trackDuration = GetTrackDuration(tracks[i]);
                    if (trackDuration > maxTrackDuration)
                    {
                        maxTrackDuration = trackDuration;
                    }
                }
            }

            // Получаем темп
            int tempo = int.Parse(((TextBox)FindName("TempoTextBox")).Text);

            // Формируем содержимое файла проекта
            var projectLines = new List<string>();

            // Первая строка: длительность микса, темп, громкость микса
            // Форматируем длительность в HH:mm:ss:fff
            string mixDurationFormatted = $"{maxTrackDuration.Hours:D2}:{maxTrackDuration.Minutes:D2}:{maxTrackDuration.Seconds:D2}:{maxTrackDuration.Milliseconds:D3}";
            projectLines.Add($"{mixDurationFormatted},{tempo},{(masterVolume * 100).ToString("F0", CultureInfo.InvariantCulture)}");

            // Для каждой дорожки добавляем информацию
            for (int i = 0; i < tracks.Count; i++)
            {
                var trackListBox = LB_Track_List[i];
                var listBoxItem = items[i];
                var stackPanel = listBoxItem.Content as StackPanel;
                if (stackPanel == null) continue;

                string trackName = "";
                string instrument = "";
                foreach (var child in stackPanel.Children)
                {
                    if (child is Grid grid)
                    {
                        foreach (var gridChild in grid.Children)
                        {
                            if (gridChild is TextBox textBox)
                            {
                                trackName = textBox.Text;
                            }
                            else if (gridChild is TextBlock textBlock)
                            {
                                instrument = textBlock.Text;
                            }
                        }
                    }
                }

                var trackDuration = GetTrackDuration(tracks[i]);
                string muteState = mutedTracks[i] ? "mute" : "unmute";
                float trackVolume = trackVolumes.ContainsKey(i) ? trackVolumes[i] * 100 : 100;
                string trackFilesList = string.Join(";", tracks[i].Select(f => System.IO.Path.GetFileName(f)));

                // Форматирование длительности дорожки в HH:mm:ss:fff
                string trackDurationFormatted = $"{trackDuration.Hours:D2}:{trackDuration.Minutes:D2}:{trackDuration.Seconds:D2}:{trackDuration.Milliseconds:D3}";
                projectLines.Add($"{trackName},{instrument},{trackDurationFormatted},{muteState},{trackVolume.ToString("F0", CultureInfo.InvariantCulture)},{trackFilesList}");
            }

            // Сохраняем файл проекта
            string projectFilePath = System.IO.Path.Combine(projectFolder, $"{projectName}.daw");
            File.WriteAllLines(projectFilePath, projectLines);
            System.Diagnostics.Debug.WriteLine($"Проект сохранён: {projectFilePath}");
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            // Вызов метода обработки закрытия с указанием, что нужно открыть MainWindow
            HandleWindowClosing(navigateToMainWindow: true);
        }

        private void pause_btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                {
                    pause_btn.Opacity = 1;

                    waveOut.Pause();
                    PauseAnimation(); // Приостанавливаем анимацию
                    System.Diagnostics.Debug.WriteLine("Воспроизведение на паузе");
                }
                else
                {
                    MessageBox.Show("Нечего ставить на паузу!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при паузе: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void skip_left_btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (waveOut != null)
                {
                    pause_btn.Opacity = 0.5;
                    refresh_btn.Opacity = 0.5;

                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;

                    bufferProvider?.ClearBuffer();
                    bufferProvider = null;

                    isLooping = false;
                    StopAnimation(); // Сбрасываем анимацию
                    System.Diagnostics.Debug.WriteLine("Воспроизведение сброшено на начало");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сбросе: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void skip_right_btn_Click(object sender, RoutedEventArgs e)
        {
            
        }

        // refresh_btn_Click
        //private void refresh_btn_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        if (tracks.Count == 0 || tracks.All(t => t.Count == 0))
        //        {
        //            MessageBox.Show("Нет записанных дорожек для воспроизведения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        if (waveOut != null)
        //        {
        //            waveOut.Stop();
        //            waveOut.Dispose();
        //            waveOut = null;
        //        }

        //        maxTrackDuration = TimeSpan.Zero;
        //        readers = new List<AudioFileReader>();
        //        volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
        //        for (int i = 0; i < tracks.Count; i++)
        //        {
        //            if (tracks[i].Count > 0 && !mutedTracks[i])
        //            {
        //                var trackDuration = GetTrackDuration(tracks[i]);
        //                if (trackDuration > maxTrackDuration)
        //                {
        //                    maxTrackDuration = trackDuration;
        //                }
        //                System.Diagnostics.Debug.WriteLine($"Длина дорожки {i + 1}: {trackDuration}");
        //            }
        //        }

        //        var renderMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
        //        renderMixer.ReadFully = true;

        //        for (int i = 0; i < tracks.Count; i++)
        //        {
        //            if (tracks[i].Count > 0 && !mutedTracks[i])
        //            {
        //                var concatenatedProvider = ConcatenateTrack(tracks[i]);
        //                var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
        //                {
        //                    Volume = trackVolumes[i]
        //                };
        //                volumeProviders[i] = volumeProvider;
        //                renderMixer.AddMixerInput(volumeProvider);
        //            }
        //        }

        //        if (!renderMixer.MixerInputs.Any())
        //        {
        //            MessageBox.Show("Все дорожки отключены или пусты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        masterVolumeProvider = new VolumeSampleProvider(renderMixer.ToWaveProvider().ToSampleProvider())
        //        {
        //            Volume = masterVolume
        //        };

        //        long totalSamples = (long)(maxTrackDuration.TotalSeconds * 44100);
        //        float[] mixBuffer = new float[totalSamples];
        //        int samplesRead = masterVolumeProvider.Read(mixBuffer, 0, (int)totalSamples);
        //        System.Diagnostics.Debug.WriteLine($"Смикшировано сэмплов: {samplesRead}/{totalSamples}");

        //        byte[] byteBuffer = new byte[samplesRead * 4];
        //        Buffer.BlockCopy(mixBuffer, 0, byteBuffer, 0, byteBuffer.Length);

        //        var bufferProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1))
        //        {
        //            BufferLength = byteBuffer.Length,
        //            ReadFully = false
        //        };
        //        bufferProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);

        //        void StartLoopingPlayback()
        //        {
        //            if (!isLooping) return;

        //            if (waveOut != null)
        //            {
        //                waveOut.Stop();
        //                waveOut.Dispose();
        //                waveOut = null;
        //            }

        //            bufferProvider.ClearBuffer();
        //            bufferProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);

        //            waveOut = new WaveOutEvent();
        //            waveOut.Init(bufferProvider);
        //            waveOut.PlaybackStopped += (s, ev) =>
        //            {
        //                System.Diagnostics.Debug.WriteLine("PlaybackStopped triggered");
        //                if (isLooping)
        //                {
        //                    StartLoopingPlayback();
        //                }
        //            };

        //            refresh_btn.Opacity = 1;
        //            pause_btn.Opacity = 0.5;

        //            waveOut.Play();
        //            System.Diagnostics.Debug.WriteLine($"Зацикленное воспроизведение начато/перезапущено, цикл: {maxTrackDuration}");
        //        }

        //        isLooping = true;
        //        StartLoopingPlayback();

        //        foreach (var reader in readers)
        //        {
        //            reader.Dispose();
        //        }
        //        readers.Clear();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("Ошибка при зацикливании микса: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        //        System.Diagnostics.Debug.WriteLine($"Ошибка в Refresh: {ex.Message}");
        //    }
        //}
        private void refresh_btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (tracks.Count == 0 || tracks.All(t => t.Count == 0))
                {
                    MessageBox.Show("Нет записанных дорожек для воспроизведения!", "Ошибка", MessageBoxButton.OK);
                    return;
                }

                if (isLooping)
                {
                    // Отключаем зацикливание
                    isLooping = false;
                    refresh_btn.Opacity = 0.5;

                    if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        waveOut.PlaybackStopped += (s, ev) =>
                        {
                            waveOut?.Stop();
                            waveOut?.Dispose();
                            waveOut = null;
                            bufferProvider?.ClearBuffer();
                            bufferProvider = null;
                            StopAnimation(); // Останавливаем анимацию
                            System.Diagnostics.Debug.WriteLine("Зацикливание отключено");
                        };
                    }
                    else
                    {
                        waveOut?.Stop();
                        waveOut?.Dispose();
                        waveOut = null;
                        bufferProvider?.ClearBuffer();
                        bufferProvider = null;
                        StopAnimation(); // Останавливаем анимацию
                        System.Diagnostics.Debug.WriteLine("Зацикливание отключено");
                    }

                    foreach (var reader in readers)
                    {
                        reader?.Dispose();
                    }
                    readers.Clear();
                    return;
                }

                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }

                if (bufferProvider != null)
                {
                    bufferProvider.ClearBuffer();
                    bufferProvider = null;
                }

                maxTrackDuration = TimeSpan.Zero;
                readers = new List<AudioFileReader>();
                volumeProviders = new List<VolumeSampleProvider>(new VolumeSampleProvider[tracks.Count]);
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].Count > 0 && !mutedTracks[i])
                    {
                        var trackDuration = GetTrackDuration(tracks[i]);
                        if (trackDuration > maxTrackDuration)
                        {
                            maxTrackDuration = trackDuration;
                        }
                    }
                }

                var renderMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
                renderMixer.ReadFully = true;

                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].Count > 0 && !mutedTracks[i])
                    {
                        var concatenatedProvider = ConcatenateTrack(tracks[i]);
                        var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
                        {
                            Volume = trackVolumes[i]
                        };
                        volumeProviders[i] = volumeProvider;
                        renderMixer.AddMixerInput(volumeProvider);
                    }
                }

                if (!renderMixer.MixerInputs.Any())
                {
                    MessageBox.Show("Все дорожки отключены или пусты!", "Ошибка", MessageBoxButton.OK);
                    return;
                }

                masterVolumeProvider = new VolumeSampleProvider(renderMixer.ToWaveProvider().ToSampleProvider())
                {
                    Volume = masterVolume
                };

                long totalSamples = (long)(maxTrackDuration.TotalSeconds * 44100);
                float[] mixBuffer = new float[totalSamples];
                int samplesRead = masterVolumeProvider.Read(mixBuffer, 0, (int)totalSamples);

                byte[] byteBuffer = new byte[samplesRead * 4];
                Buffer.BlockCopy(mixBuffer, 0, byteBuffer, 0, byteBuffer.Length);

                bufferProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1))
                {
                    BufferLength = byteBuffer.Length,
                    ReadFully = false
                };
                bufferProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);

                void StartLoopingPlayback()
                {
                    if (!isLooping) return;

                    if (waveOut != null)
                    {
                        waveOut.Stop();
                        waveOut.Dispose();
                        waveOut = null;
                    }

                    bufferProvider.ClearBuffer();
                    bufferProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);

                    waveOut = new WaveOutEvent();
                    waveOut.Init(bufferProvider);
                    waveOut.PlaybackStopped += (s, ev) =>
                    {
                        if (isLooping)
                        {
                            StartLoopingPlayback();
                        }
                        else
                        {
                            waveOut?.Stop();
                            waveOut?.Dispose();
                            waveOut = null;
                            bufferProvider?.ClearBuffer();
                            bufferProvider = null;
                            StopAnimation(); // Останавливаем анимацию
                        }
                    };

                    refresh_btn.Opacity = 1;
                    pause_btn.Opacity = 0.5;

                    waveOut.Play();

                    // Запускаем анимацию
                    InitializeAnimation();
                    StartAnimation();
                }

                isLooping = true;
                StartLoopingPlayback();

                foreach (var reader in readers)
                {
                    reader?.Dispose();
                }
                readers.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при зацикливании микса: " + ex.Message);
            }
        }

        /// <summary>
        /// Метод рендерит
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаём папку Songs, если она не существует
                string songsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
                if (!Directory.Exists(songsFolder))
                {
                    Directory.CreateDirectory(songsFolder);
                }

                if (tracks.Count == 0 || tracks.All(t => t.Count == 0))
                {
                    MessageBox.Show("Нет записанных дорожек для рендера!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                waveOut?.Stop();
                waveOut?.Dispose();
                waveOut = null;

                maxTrackDuration = TimeSpan.Zero;
                readers = new List<AudioFileReader>();
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].Count > 0 && !mutedTracks[i])
                    {
                        var trackDuration = GetTrackDuration(tracks[i]);
                        if (trackDuration > maxTrackDuration)
                        {
                            maxTrackDuration = trackDuration;
                        }
                    }
                }

                if (maxTrackDuration == TimeSpan.Zero)
                {
                    MessageBox.Show("Все дорожки отключены или пусты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var renderMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
                renderMixer.ReadFully = true;

                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].Count > 0 && !mutedTracks[i])
                    {
                        var concatenatedProvider = ConcatenateTrack(tracks[i]);
                        var volumeProvider = new VolumeSampleProvider(concatenatedProvider)
                        {
                            Volume = trackVolumes[i]
                        };
                        renderMixer.AddMixerInput(volumeProvider);
                    }
                }

                masterVolumeProvider = new VolumeSampleProvider(renderMixer.ToWaveProvider().ToSampleProvider())
                {
                    Volume = masterVolume
                };

                // Предлагаем имя файла по умолчанию
                string defaultFileName = "rendered_mix.wav";
                int fileIndex = 1;
                string outputFile = System.IO.Path.Combine(songsFolder, defaultFileName);
                string favoritesFolderPath = System.IO.Path.Combine(songsFolder, "Favorites");
                string outputFileFav = System.IO.Path.Combine(favoritesFolderPath, defaultFileName);
                MessageBox.Show($"outputFileFav = {outputFileFav}");
                while (File.Exists(outputFile) || File.Exists(outputFileFav))
                {
                    defaultFileName = $"rendered_mix_{fileIndex++}.wav";
                    outputFile = System.IO.Path.Combine(songsFolder, defaultFileName);
                    outputFileFav = System.IO.Path.Combine(favoritesFolderPath, defaultFileName);
                }

                // Показываем диалоговое окно для выбора имени файла и формата
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    InitialDirectory = songsFolder,
                    FileName = defaultFileName,
                    Filter = "WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3",
                    DefaultExt = "wav",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return; // Пользователь отменил сохранение
                }

                // Получаем путь и формат файла
                string selectedFilePath = saveFileDialog.FileName;

                if (string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    MessageBox.Show("Имя песни не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string songsFolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
                string newSongPathInFavorites = System.IO.Path.Combine(favoritesFolderPath, defaultFileName);

                // Проверяем, не существует ли уже песня с таким именем
                if (File.Exists(selectedFilePath) || File.Exists(newSongPathInFavorites))
                {
                    MessageBox.Show("Песня с таким именем уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }


                bool saveAsMp3 = System.IO.Path.GetExtension(selectedFilePath).ToLower() == ".mp3";

                // Временный файл WAV (если нужен MP3, сначала сохраняем как WAV, потом конвертируем)
                string tempWavFile = saveAsMp3 ? System.IO.Path.Combine(songsFolder, "temp_rendered_mix.wav") : selectedFilePath;

                // Рендеринг в WAV
                using (var writer = new WaveFileWriter(tempWavFile, renderMixer.WaveFormat))
                {
                    int bufferSize = 44100;
                    float[] buffer = new float[bufferSize];
                    long totalSamples = (long)(maxTrackDuration.TotalSeconds * 44100);
                    long samplesWritten = 0;

                    while (samplesWritten < totalSamples)
                    {
                        int samplesToRead = (int)Math.Min(bufferSize, totalSamples - samplesWritten);
                        int samplesRead = masterVolumeProvider.Read(buffer, 0, samplesToRead);
                        if (samplesRead == 0) break;
                        writer.WriteSamples(buffer, 0, samplesRead);
                        samplesWritten += samplesRead;
                        System.Diagnostics.Debug.WriteLine($"Записано сэмплов: {samplesWritten}/{totalSamples}");
                    }
                }

                // Если пользователь выбрал MP3, конвертируем WAV в MP3
                if (saveAsMp3)
                {
                    try
                    {
                        using (var reader = new WaveFileReader(tempWavFile))
                        using (var mp3Writer = new LameMP3FileWriter(selectedFilePath, reader.WaveFormat, LAMEPreset.STANDARD))
                        {
                            reader.CopyTo(mp3Writer);
                        }

                        // Удаляем временный WAV-файл
                        File.Delete(tempWavFile);
                    }
                    catch (Exception ex)
                    {
                        // Если произошла ошибка при конвертации, удаляем временный файл и показываем ошибку
                        if (File.Exists(tempWavFile))
                        {
                            File.Delete(tempWavFile);
                        }
                        throw new Exception($"Ошибка при конвертации в MP3: {ex.Message}", ex);
                    }
                }

                foreach (var reader in readers)
                {
                    reader.Dispose();
                }
                readers.Clear();

                System.Diagnostics.Debug.WriteLine($"Рендер завершен");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при рендере микса: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка в Render: {ex.Message}");
            }
        }

        /// <summary>
        /// Метод открытия панели микшера
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowMixerPanelBtn_Click(object sender, RoutedEventArgs e)
        {
            mixerTabButton.Visibility = Visibility.Collapsed;
            mixerPanel.Visibility = Visibility.Visible; // Панель видна сразу

            // Анимация изменения высоты строк
            var row2 = MainGrid.RowDefinitions[1]; // ScrollViewer
            var row3 = MainGrid.RowDefinitions[2]; // Панель кнопок

            RowDefinitionHeightAnimation row2Animation = new RowDefinitionHeightAnimation
            {
                From = row2.ActualHeight,
                To = row2.ActualHeight - 300, // Уменьшаем на высоту панели
                Duration = TimeSpan.FromSeconds(0.3)
            };
            RowDefinitionHeightAnimation row3Animation = new RowDefinitionHeightAnimation
            {
                From = 40,
                To = 340, // 40 (кнопки) + 200 (панель)
                Duration = TimeSpan.FromSeconds(0.3)
            };

            row2.BeginAnimation(RowDefinition.HeightProperty, row2Animation);
            row3.BeginAnimation(RowDefinition.HeightProperty, row3Animation);
        }

        private void ShowMixerPanelMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            if (mixerPanel.Visibility == Visibility.Visible)
            {
                CloseMixer();
            }
            else
            {
                mixerTabButton.Visibility = Visibility.Collapsed;
                mixerPanel.Visibility = Visibility.Visible;

                // Анимация изменения высоты строк
                var row2 = MainGrid.RowDefinitions[1]; // ScrollViewer
                var row3 = MainGrid.RowDefinitions[2]; // Панель кнопок
                RowDefinitionHeightAnimation row2Animation = new RowDefinitionHeightAnimation
                {
                    From = row2.ActualHeight,
                    To = row2.ActualHeight - 300, // Уменьшаем на высоту панели
                    Duration = TimeSpan.FromSeconds(0)
                };
                RowDefinitionHeightAnimation row3Animation = new RowDefinitionHeightAnimation
                {
                    From = 40,
                    To = 340, // 40 (кнопки) + 200 (панель)
                    Duration = TimeSpan.FromSeconds(0)
                };
                row2.BeginAnimation(RowDefinition.HeightProperty, row2Animation);
                row3.BeginAnimation(RowDefinition.HeightProperty, row3Animation);
            }
        }

        private void MinimizeMixerBtn_Click(object sender, RoutedEventArgs e)
        {
            // Анимация изменения высоты строк
            var row2 = MainGrid.RowDefinitions[1]; // ScrollViewer
            var row3 = MainGrid.RowDefinitions[2]; // Панель кнопок

            RowDefinitionHeightAnimation row2Animation = new RowDefinitionHeightAnimation
            {
                From = row2.ActualHeight,
                To = originalRow2Height, // Возвращаем исходную высоту
                Duration = TimeSpan.FromSeconds(0.3)
            };
            RowDefinitionHeightAnimation row3Animation = new RowDefinitionHeightAnimation
            {
                From = 340,
                To = 40, // Возвращаем исходную высоту
                Duration = TimeSpan.FromSeconds(0.3)
            };

            // Скрываем панель и показываем кнопку после завершения анимации
            row3Animation.Completed += (s, ev) =>
            {
                mixerPanel.Visibility = Visibility.Collapsed;
                mixerTabButton.Visibility = Visibility.Visible;
            };

            row2.BeginAnimation(RowDefinition.HeightProperty, row2Animation);
            row3.BeginAnimation(RowDefinition.HeightProperty, row3Animation);
        }

        private void CloseMixerBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseMixer();
        }

        private void CloseMixer()
        {
            mixerPanel.Visibility = Visibility.Collapsed;
            mixerTabButton.Visibility = Visibility.Collapsed;

            // Возвращаем высоту строк к исходным значениям (как в XAML)
            var row2 = MainGrid.RowDefinitions[1]; // ScrollViewer
            var row3 = MainGrid.RowDefinitions[2]; // Панель кнопок

            RowDefinitionHeightAnimation row2Animation = new RowDefinitionHeightAnimation
            {
                From = row2.ActualHeight,
                To = originalRow2Height, // Возвращаем исходную высоту
                Duration = TimeSpan.FromSeconds(0)
            };
            RowDefinitionHeightAnimation row3Animation = new RowDefinitionHeightAnimation
            {
                From = 340,
                To = 40, // Возвращаем исходную высоту
                Duration = TimeSpan.FromSeconds(0)
            };
            row2.BeginAnimation(RowDefinition.HeightProperty, row2Animation);
            row3.BeginAnimation(RowDefinition.HeightProperty, row3Animation);
        }

        public class RowDefinitionHeightAnimation : AnimationTimeline
        {
            public static readonly DependencyProperty FromProperty = DependencyProperty.Register("From", typeof(double), typeof(RowDefinitionHeightAnimation));
            public static readonly DependencyProperty ToProperty = DependencyProperty.Register("To", typeof(double), typeof(RowDefinitionHeightAnimation));

            public double From
            {
                get => (double)GetValue(FromProperty);
                set => SetValue(FromProperty, value);
            }

            public double To
            {
                get => (double)GetValue(ToProperty);
                set => SetValue(ToProperty, value);
            }

            public override Type TargetPropertyType => typeof(GridLength);

            protected override Freezable CreateInstanceCore() => new RowDefinitionHeightAnimation();

            public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
            {
                double fromValue = From;
                double toValue = To;
                double progress = animationClock.CurrentProgress.Value;
                double newValue = fromValue + (toValue - fromValue) * progress;
                return new GridLength(newValue, GridUnitType.Pixel);
            }
        }

        private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            masterVolume = (float)e.NewValue;
            System.Diagnostics.Debug.WriteLine($"Общая громкость изменена на {masterVolume}");

            if (masterVolumeProvider != null && waveOut != null && (waveOut.PlaybackState == PlaybackState.Playing || waveOut.PlaybackState == PlaybackState.Paused))
            {
                masterVolumeProvider.Volume = masterVolume;
            }
        }

        private void TempoTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            save_btn.Opacity = 1;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox tb = (TextBox)sender;
                if (tb.Name == "TempoTextBox" && isMetronomeOn)
                {
                    MetronomeBtn_Click(sender, e);
                    MetronomeBtn_Click(sender, e);
                }
                //Keyboard.ClearFocus();
                scrollViewer.Focus(); // Перемещаем фокус
            }
        }

        private void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(SelectedInputDeviceIndex, SelectedOutputDeviceIndex, UseAsio, AsioDriverName);
            if (settingsWindow.ShowDialog() == true)
            {
                System.Diagnostics.Debug.WriteLine($"Настройки обновлены: Ввод={SelectedInputDeviceIndex}, Вывод={SelectedOutputDeviceIndex}, ASIO={UseAsio}, Драйвер={AsioDriverName}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }
    }
}

public class AudioSpectrum
{
    private WaveInEvent waveIn;
    private Canvas canvas;
    private int bandCount = 4; // Можно настроить количество полос
    private float[] bandValues;

    public AudioSpectrum(Canvas spectrumCanvas)
    {
        canvas = spectrumCanvas;
        bandValues = new float[bandCount];

        waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = 50 // Уменьшаем задержку для лучшего обновления
        };

        waveIn.DataAvailable += OnDataAvailable;
    }

    public void Start() => waveIn.StartRecording();
    public void Stop() => waveIn.StopRecording();

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        int fftSize = 1024; // Достаточно для качественного анализа
        Complex[] complexData = new Complex[fftSize];

        for (int i = 0; i < fftSize && i * 2 < e.Buffer.Length; i++)
        {
            complexData[i].X = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
            complexData[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2), complexData);

        for (int i = 0; i < bandCount; i++)
        {
            int index = i * (fftSize / bandCount);
            bandValues[i] = Math.Abs(complexData[index].X);
        }

        UpdateSpectrum();
    }

    private void UpdateSpectrum()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            canvas.Children.Clear();
            double barWidth = canvas.Width / bandCount;

            for (int i = 0; i < bandCount; i++)
            {
                double amplitude = Math.Max(10, bandValues[i] * 500); // Усиливаем чувствительность
                amplitude = Math.Min(amplitude, canvas.Height);

                Rectangle bar = new Rectangle
                {
                    Width = barWidth - 5,
                    Height = amplitude,
                    Fill = new SolidColorBrush(Color.FromRgb(0, 180, 255)), // Цвет полос
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                Canvas.SetLeft(bar, i * barWidth);
                Canvas.SetBottom(bar, 0);
                canvas.Children.Add(bar);
            }
        });
    }
}

