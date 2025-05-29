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
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace MusicCreate.Pages
{
    /// <summary>
    /// Логика взаимодействия для SongsListPage.xaml
    /// </summary>
    public partial class SongsListPage : Page
    {
        public ObservableCollection<Song> Songs { get; set; }
        public ObservableCollection<Song> FilteredSongs { get; set; }
        public Song SelectedSong { get; set; }
        private WaveOutEvent waveOut;
        private AudioFileReader currentReader;
        private string songsFolder;
        bool IsFavoriteSongList = false;
        public SongsListPage()
        {
            InitializeComponent();
            Songs = new ObservableCollection<Song>();
            FilteredSongs = new ObservableCollection<Song>();
            DataContext = this;

            // Путь к папке Songs
            songsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
            LoadSongs();

            // Изначально отображаем все песни
            SongsListView.ItemsSource = FilteredSongs;

            var SortingOptions = new ObservableCollection<SortOption>
            {
                new SortOption { Text = "По имени (А-Я, A-Z)", ImagePath = "/Images/icon-sort-az.png" },
                new SortOption { Text = "По имени (Я-А, Z-A)", ImagePath = "/Images/icon-sort-za.png" },
                new SortOption { Text = "По дате создания (сначала старые)", ImagePath = "/Images/icon-clock-up.png" },
                new SortOption { Text = "По дате создания (сначала новые)", ImagePath = "/Images/icon-clock-down.png" }
            };

            cb_sort.ItemsSource = SortingOptions;
            cb_sort.SelectedIndex = 0;
        }
        // Загрузка списка песен
        private void LoadSongs()
        {
            Songs.Clear();
            FilteredSongs.Clear();
            if (!Directory.Exists(songsFolder))
            {
                Directory.CreateDirectory(songsFolder); // Создаём папку Songs, если не существует
                return;
            }

            // Создаём папку Favorites, если она не существует
            string favoritesFolder = System.IO.Path.Combine(songsFolder, "Favorites");
            if (!Directory.Exists(favoritesFolder))
            {
                Directory.CreateDirectory(favoritesFolder);
                System.Diagnostics.Debug.WriteLine("Папка Favorites создана для песен");
            }

            foreach (var file in Directory.GetFiles(songsFolder, "*.wav", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(songsFolder, "*.mp3", SearchOption.AllDirectories)))
            {
                try
                {
                    using (var reader = new AudioFileReader(file))
                    {
                        var song = new Song
                        {
                            FilePath = file,
                            FileName = System.IO.Path.GetFileNameWithoutExtension(file),
                            FileNameWithExtension = System.IO.Path.GetFileName(file),
                            Extension = System.IO.Path.GetExtension(file).ToLower(),
                            Duration = reader.TotalTime,
                            IsFavorite = file.StartsWith(favoritesFolder, StringComparison.OrdinalIgnoreCase),
                            IsPlaying = false
                        };
                        Songs.Add(song);
                        FilteredSongs.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла {file}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Метод воспроизведения песни
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Song song)
            {
                try
                {
                    // Останавливаем текущее воспроизведение и освобождаем ресурсы
                    StopPlayback();

                    // Создаем новый AudioFileReader и сохраняем его
                    currentReader = new AudioFileReader(song.FilePath);
                    waveOut = new WaveOutEvent();
                    waveOut.Init(currentReader);
                    waveOut.Play();

                    song.IsPlaying = true;

                    // Подписываемся на событие остановки воспроизведения
                    waveOut.PlaybackStopped += (s, args) =>
                    {
                        StopPlayback();
                        song.IsPlaying = false; // Сбрасываем флаг
                    };
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при воспроизведении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Метод остановки воспроизведения и освобождения ресурсов
        /// </summary>
        private void StopPlayback()
        {
            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }

            if (currentReader != null)
            {
                currentReader.Dispose();
                currentReader = null;
            }
        }

        // Удаление песни
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Song song)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить {song.FileNameWithExtension}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        StopPlayback();

                        File.Delete(song.FilePath);
                        Songs.Remove(song);
                        FilteredSongs.Remove(song);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Открытие папки с файлами
        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", songsFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчики для поиска
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Поиск")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = Brushes.Black;
            }
            Search_border.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#1695A3");
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Поиск";
                SearchTextBox.Foreground = Brushes.Gray;
                SongsListView.ItemsSource = FilteredSongs;
            }
            Search_border.BorderBrush = Brushes.Transparent;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchTextBox.Text != "Поиск")
            {
                FilterSongs(SearchTextBox.Text);
            }
        }

        private void FilterSongs(string searchText)
        {
            FilteredSongs.Clear();
            var filtered = Songs.Where(s => s.FileNameWithExtension.ToLower().Contains(searchText.ToLower()));
            foreach (var song in filtered)
            {
                FilteredSongs.Add(song);
            }
            SongsListView.ItemsSource = FilteredSongs;

            if (FilteredSongs.Any())
            {
                UpdateHighlightedText(searchText);
            }
        }

        private void UpdateHighlightedText(string searchText)
        {
            foreach (var item in SongsListView.Items)
            {
                SongsListView.UpdateLayout(); // Обновляем контейнеры

                ListViewItem container = SongsListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                TextBlock textBlock = FindVisualChild<TextBlock>(container);
                if (textBlock == null) continue;

                string songFileNameWithExtension = (item as Song)?.FileNameWithExtension ?? "";
                textBlock.Inlines.Clear();

                if (string.IsNullOrWhiteSpace(searchText) || searchText.ToLower() == "поиск")
                {
                    textBlock.Inlines.Add(new Run(songFileNameWithExtension));
                    continue;
                }

                int index = songFileNameWithExtension.ToLower().IndexOf(searchText.ToLower());
                if (index < 0)
                {
                    textBlock.Inlines.Add(new Run(songFileNameWithExtension));
                    continue;
                }

                if (index > 0)
                    textBlock.Inlines.Add(new Run(songFileNameWithExtension.Substring(0, index)));

                Span highlightedSpan = new Span(new Run(songFileNameWithExtension.Substring(index, searchText.Length)))
                {
                    Background = Brushes.LightBlue
                };

                textBlock.Inlines.Add(highlightedSpan);

                if (index + searchText.Length < songFileNameWithExtension.Length)
                    textBlock.Inlines.Add(new Run(songFileNameWithExtension.Substring(index + searchText.Length)));
            }
        }


        // Функция для поиска дочернего элемента внутри шаблона
        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                    return (T)child;
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Song song = button.Tag as Song;

            if (song == null)
            {
                MessageBox.Show("Не удалось определить песню.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StopPlayback();

            string songsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
            string favoritesFolder = System.IO.Path.Combine(songsFolder, "Favorites");
            string oldSongPath = song.FilePath;
            string newSongPath;

            try
            {
                if (song.IsFavorite)
                {
                    // Перемещаем из Favorites обратно в Songs
                    newSongPath = System.IO.Path.Combine(songsFolder, song.FileNameWithExtension);
                    File.Move(oldSongPath, newSongPath);
                    song.IsFavorite = false;
                    System.Diagnostics.Debug.WriteLine($"Песня {song.FileNameWithExtension} удалена из избранного");
                }
                else
                {
                    // Перемещаем в Favorites
                    newSongPath = System.IO.Path.Combine(favoritesFolder, song.FileNameWithExtension);
                    File.Move(oldSongPath, newSongPath);
                    song.IsFavorite = true;
                    System.Diagnostics.Debug.WriteLine($"Песня {song.FileNameWithExtension} добавлена в избранное");
                }

                // Обновляем путь к файлу песни
                song.FilePath = newSongPath;

                // Обновляем ListView
                SongsListView.ItemsSource = null;
                if (IsFavoriteSongList)
                {
                    SongsListView.ItemsSource = FilteredSongs.Where(n => n.IsFavorite);
                }
                else
                {
                    SongsListView.ItemsSource = FilteredSongs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении статуса избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при изменении избранного для {song.FileNameWithExtension}: {ex.Message}");
            }
        }

        // Обработчик кнопки "Троеточие" для открытия контекстного меню
        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        
        // Метод для изменения названия композиции
        private void RenameSong_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;

            ContextMenu contextMenu = menuItem.Parent as ContextMenu;
            if (contextMenu == null) return;

            Button button = contextMenu.PlacementTarget as Button;
            if (button == null) return;

            Song song = button.Tag as Song;
            if (song == null)
            {
                MessageBox.Show("Не удалось определить песню для переименования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Создаём окно для ввода нового имени
            var inputDialog = new Window
            {
                Title = "Переименование песни",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBlock = new TextBlock { Text = "Введите новое название песни:" };
            var textBox = new TextBox { Text = System.IO.Path.GetFileNameWithoutExtension(song.FileNameWithExtension), Margin = new Thickness(0, 5, 0, 10) };
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
                return; // Пользователь отменил переименование
            }

            string newSongName = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newSongName))
            {
                MessageBox.Show("Имя песни не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Сохраняем оригинальное расширение файла
            string extension = song.Extension; // .wav или .mp3
            if (!newSongName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                newSongName += extension;
            }

            string songsFolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
            string favoritesFolderPath = System.IO.Path.Combine(songsFolderPath, "Favorites");

            string newSongPathInSongs = System.IO.Path.Combine(songsFolderPath, newSongName);
            string newSongPathInFavorites = System.IO.Path.Combine(favoritesFolderPath, newSongName);

            // Проверяем, не существует ли уже песня с таким именем
            if ((File.Exists(newSongPathInSongs) && newSongPathInSongs != song.FilePath) ||
                (File.Exists(newSongPathInFavorites) && newSongPathInFavorites != song.FilePath))
            {
                MessageBox.Show("Песня с таким именем уже существует в папке Songs или Favorites!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                StopPlayback();

                string newSongPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(song.FilePath), newSongName);
                bool success = TryFileOperation(() => File.Move(song.FilePath, newSongPath), song.FilePath);
                if (!success)
                {
                    MessageBox.Show("Не удалось переименовать файл: он все еще занят.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Песня переименована: {song.FilePath} -> {newSongPath}");

                song.FilePath = newSongPath;
                song.FileNameWithExtension = newSongName;
                song.FileName = System.IO.Path.GetFileNameWithoutExtension(newSongName);

                SongsListView.ItemsSource = null;
                SongsListView.ItemsSource = FilteredSongs;
                System.Diagnostics.Debug.WriteLine($"Песня переименована в списке: {newSongName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переименовании песни: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при переименовании песни {song.FileNameWithExtension}: {ex.Message}");
            }
        }
        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            // Находим ComboBox, который является родителем кнопки
            var button = sender as Button;
            var comboBox = button?.TemplatedParent as ComboBox;

            if (comboBox != null)
            {
                // Переключаем состояние выпадающего списка
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            }
        }

        private void cb_sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_sort.SelectedItem is SortOption selectedOption)
            {
                switch (selectedOption.Text)
                {
                    case "По имени (А-Я, A-Z)":
                        FilteredSongs = new ObservableCollection<Song>(FilteredSongs.OrderBy(s => s.FileNameWithExtension));
                        break;
                    case "По имени (Я-А, Z-A)":
                        FilteredSongs = new ObservableCollection<Song>(FilteredSongs.OrderByDescending(s => s.FileNameWithExtension));
                        break;
                    case "По дате создания (сначала старые)":
                        FilteredSongs = new ObservableCollection<Song>(FilteredSongs.OrderBy(s => File.GetCreationTime(s.FilePath)));
                        break;
                    case "По дате создания (сначала новые)":
                        FilteredSongs = new ObservableCollection<Song>(FilteredSongs.OrderByDescending(s => File.GetCreationTime(s.FilePath)));
                        break;
                }

                SongsListView.ItemsSource = null; // Очищаем старый список
                SongsListView.ItemsSource = FilteredSongs; // Устанавливаем обновленный список
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox tb = (TextBox)sender;
                SearchTextBox_LostFocus(sender, e);
                Keyboard.ClearFocus();
                AllButton.Focus();
            }
        }

        private void AllSongsButtonClick(object sender, RoutedEventArgs e)
        {
            AllButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#6ECCD7");
            FavoriteButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#76DDE8");
            SongsListView.ItemsSource = FilteredSongs;
            TitleTextBlock.Text = "Список песен";
            IsFavoriteSongList = false;
        }

        private void FavoriteSongsButtonClick(object sender, RoutedEventArgs e)
        {
            AllButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#76DDE8");
            FavoriteButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#6ECCD7");
            SongsListView.ItemsSource = FilteredSongs.Where(n => n.IsFavorite == true);
            TitleTextBlock.Text = "Список избранных песен";
            IsFavoriteSongList = true;
        }
        private bool TryFileOperation(Action fileOperation, string filePath, int maxRetries = 3, int delayMs = 100)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    fileOperation();
                    return true;
                }
                catch (IOException ex) when (ex.Message.Contains("файл занят другим процессом"))
                {
                    if (i == maxRetries - 1) return false;
                    StopPlayback();
                    System.Threading.Thread.Sleep(delayMs);
                }
            }
            return false;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Song song)
            {
                StopPlayback();
                song.IsPlaying = false;
            }
        }
    }

    // Конвертер для чередования цвета фона
    public static class AlternationIndexToBackgroundConverter
    {
        public static class Converter
        {
            public static IValueConverter Instance { get; } = new InnerConverter();

            private class InnerConverter : IValueConverter
            {
                public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
                {
                    if (value is int index)
                    {
                        return index % 2 == 0 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3E4F1")) : Brushes.Transparent;
                    }
                    return Brushes.Transparent;
                }

                public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
                {
                    throw new NotImplementedException();
                }
            }
        }
    }

    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
