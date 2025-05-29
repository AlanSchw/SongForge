using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Globalization; // Добавляем для CultureInfo
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Data;
using static System.Net.Mime.MediaTypeNames;
using System.ComponentModel;

namespace MusicCreate.Pages
{
    /// <summary>
    /// Логика взаимодействия для ProjectsListPage.xaml
    /// </summary>
    public partial class ProjectsListPage : Page
    {
        private List<Project> projectsList;
        MainWindow mw;
        bool IsFavoriteProjectList = false;

        public ProjectsListPage(MainWindow mw)
        {
            InitializeComponent();
            LoadProjects();
            this.mw = mw;

            var SortingOptions = new ObservableCollection<SortOption>
            {
                new SortOption { Text = "По имени (А-Я, A-Z)", ImagePath = "/Images/icon-sort-az.png" },
                new SortOption { Text = "По имени (Я-А, Z-A)", ImagePath = "/Images/icon-sort-za.png" },
                new SortOption { Text = "По дате создания (сначала старые)", ImagePath = "/Images/icon-clock-up.png" },
                new SortOption { Text = "По дате создания (сначала новые)", ImagePath = "/Images/icon-clock-down.png" },
                new SortOption { Text = "По дате изменения (сначала старые)", ImagePath = "/Images/icon-clock-up.png" },
                new SortOption { Text = "По дате изменения (сначала новые)", ImagePath = "/Images/icon-clock-down.png" }
            };

            cb_sort.ItemsSource = SortingOptions;
            cb_sort.SelectedIndex = 0;
        }


        private void LoadProjects()
        {
            projectsList = new List<Project>();

            // Путь к папке Projects
            string projectsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            if (!Directory.Exists(projectsFolder))
            {
                System.Diagnostics.Debug.WriteLine("Папка Projects не существует");
                listView_projects.ItemsSource = projectsList;
                return;
            }

            // Создаём папку Favorites, если она не существует
            string favoritesFolder = System.IO.Path.Combine(projectsFolder, "Favorites");
            if (!Directory.Exists(favoritesFolder))
            {
                Directory.CreateDirectory(favoritesFolder);
                System.Diagnostics.Debug.WriteLine("Папка Favorites создана");
            }

            // Собираем все папки проектов, исключая TemporaryTracks
            var projectFolders = Directory.GetDirectories(projectsFolder, "*", SearchOption.AllDirectories)
                .Where(dir => !dir.EndsWith("TemporaryTracks"))
                .ToList();

            foreach (var projectFolder in projectFolders)
            {
                string projectName = System.IO.Path.GetFileName(projectFolder);
                string projectFilePath = System.IO.Path.Combine(projectFolder, $"{projectName}.daw");

                if (File.Exists(projectFilePath))
                {
                    try
                    {
                        // Читаем первую строку файла .daw
                        string[] lines = File.ReadAllLines(projectFilePath);
                        if (lines.Length == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Файл {projectFilePath} пуст");
                            continue;
                        }

                        // Первая строка: длительность, темп, громкость
                        string[] projectData = lines[0].Split(',');
                        if (projectData.Length < 3)
                        {
                            System.Diagnostics.Debug.WriteLine($"Некорректный формат файла {projectFilePath}");
                            continue;
                        }

                        // Парсим длительность
                        TimeSpan duration;
                        if (!TimeSpan.TryParseExact(projectData[0], @"hh\:mm\:ss\:fff", CultureInfo.InvariantCulture, out duration))
                        {
                            System.Diagnostics.Debug.WriteLine($"Некорректный формат длительности в файле {projectFilePath}: {projectData[0]}");
                            continue;
                        }

                        int bpm = int.Parse(projectData[1]);

                        // Получаем даты создания и изменения
                        DateTime creationTime = File.GetCreationTime(projectFilePath);
                        DateTime lastWriteTime = File.GetLastWriteTime(projectFilePath);

                        // Определяем, находится ли проект в Favorites
                        bool isFavorite = projectFolder.StartsWith(favoritesFolder, StringComparison.OrdinalIgnoreCase);

                        // Создаём объект Project
                        Project projectInfo = new Project
                        {
                            Name = projectName,
                            Duration = duration,
                            Bpm = bpm,
                            Date_time_create = creationTime,
                            Date_time_change = lastWriteTime,
                            ProjectPath = projectFilePath,
                            IsFavorite = isFavorite
                        };

                        projectsList.Add(projectInfo);
                        System.Diagnostics.Debug.WriteLine($"Добавлен проект: {projectName}, Путь: {projectFilePath}, Избранное: {isFavorite}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при чтении файла {projectFilePath}: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Файл проекта {projectFilePath} не найден");
                }
            }

            // Привязываем список к ListView
            listView_projects.ItemsSource = projectsList;
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox.Text == "Поиск")
            {
                textBox.Text = "";
                textBox.Foreground = Brushes.Black;
            }
            Search_border.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#1695A3");
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Поиск";
                textBox.Foreground = Brushes.Gray;
            }
            Search_border.BorderBrush = Brushes.Transparent;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string searchText = textBox.Text.ToLower();
            if (searchText != "поиск")
            {
                var filteredProjects = projectsList
                .Where(p => p.Name.ToLower().Contains(searchText))
                .ToList();
                listView_projects.ItemsSource = filteredProjects;

                if (filteredProjects.Any())
                {
                    UpdateHighlightedText(searchText);
                }
            }
        }

        private void UpdateHighlightedText(string searchText)
        {
            foreach (var item in listView_projects.Items)
            {
                listView_projects.UpdateLayout(); // Обновляем контейнеры

                ListViewItem container = listView_projects.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                TextBlock textBlock = FindVisualChild<TextBlock>(container);
                if (textBlock == null) continue;

                string projectName = (item as Project)?.Name ?? "";
                textBlock.Inlines.Clear();

                if (string.IsNullOrWhiteSpace(searchText) || searchText == "поиск")
                {
                    textBlock.Inlines.Add(new Run(projectName));
                    continue;
                }

                int index = projectName.ToLower().IndexOf(searchText.ToLower());
                if (index < 0)
                {
                    textBlock.Inlines.Add(new Run(projectName));
                    continue;
                }

                if (index > 0)
                    textBlock.Inlines.Add(new Run(projectName.Substring(0, index)));

                Span highlightedSpan = new Span(new Run(projectName.Substring(index, searchText.Length)))
                {
                    Background = Brushes.LightBlue
                };
                //highlightedSpan.TextDecorations = TextDecorations.Underline; // Альтернативное выделение

                textBlock.Inlines.Add(highlightedSpan);

                if (index + searchText.Length < projectName.Length)
                    textBlock.Inlines.Add(new Run(projectName.Substring(index + searchText.Length)));
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


        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string projectsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            if (Directory.Exists(projectsFolder))
            {
                Process.Start("explorer.exe", projectsFolder);
            }
            else
            {
                MessageBox.Show("Папка с проектами не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //private void HomeButton_Click(object sender, RoutedEventArgs e)
        //{
        //    // Переход на главную страницу
        //    NavigationService?.Navigate(new Uri("/Pages/MainPage.xaml", UriKind.Relative));
        //}

        private void listView_projects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = listView_projects.SelectedItem as Project;
            ProjectCreateWindow projectCreateWindow = new ProjectCreateWindow(selected);
            projectCreateWindow.Show();
            mw.Close();
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            Project project = new Project
            {
                Name = "New Project",
                Duration = TimeSpan.Zero,
                Bpm = 120,
                Date_time_create = DateTime.Now,
                Date_time_change = DateTime.Now
            };
            ProjectCreateWindow projectCreateWindow = new ProjectCreateWindow(project);
            projectCreateWindow.Show();
            mw.Close();
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Project project = button.Tag as Project;

            if (project == null)
            {
                MessageBox.Show("Не удалось определить проект.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string projectsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            string favoritesFolder = System.IO.Path.Combine(projectsFolder, "Favorites");
            string oldProjectFolder = System.IO.Path.GetDirectoryName(project.ProjectPath);
            string newProjectFolder;

            try
            {
                if (project.IsFavorite)
                {
                    // Перемещаем из Favorites обратно в Projects
                    newProjectFolder = System.IO.Path.Combine(projectsFolder, project.Name);
                    Directory.Move(oldProjectFolder, newProjectFolder);
                    project.IsFavorite = false;
                    System.Diagnostics.Debug.WriteLine($"Проект {project.Name} удалён из избранного");
                }
                else
                {
                    // Перемещаем в Favorites
                    newProjectFolder = System.IO.Path.Combine(favoritesFolder, project.Name);
                    Directory.Move(oldProjectFolder, newProjectFolder);
                    project.IsFavorite = true;
                    System.Diagnostics.Debug.WriteLine($"Проект {project.Name} добавлен в избранное");
                }

                // Обновляем путь к файлу проекта
                string newProjectFilePath = System.IO.Path.Combine(newProjectFolder, $"{project.Name}.daw");
                project.ProjectPath = newProjectFilePath;

                // Обновляем ListView
                listView_projects.ItemsSource = null;
                if (IsFavoriteProjectList)
                {
                    listView_projects.ItemsSource = projectsList.Where(n => n.IsFavorite == true);
                }
                else
                {
                    listView_projects.ItemsSource = projectsList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении статуса избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при изменении избранного для {project.Name}: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Project project = button.Tag as Project;

            if (project == null)
            {
                MessageBox.Show("Не удалось определить проект для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show($"Вы уверены, что хотите удалить проект '{project.Name}'?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // Путь к папке проекта
                string projectFolder = System.IO.Path.GetDirectoryName(project.ProjectPath);
                if (Directory.Exists(projectFolder))
                {
                    Directory.Delete(projectFolder, true); // Удаляем папку со всем содержимым
                    System.Diagnostics.Debug.WriteLine($"Папка проекта удалена: {projectFolder}");
                }

                // Удаляем проект из списка и обновляем ListView
                projectsList.Remove(project);
                listView_projects.ItemsSource = null; // Сбрасываем ItemsSource
                listView_projects.ItemsSource = projectsList; // Перепривязываем
                System.Diagnostics.Debug.WriteLine($"Проект {project.Name} удалён из списка");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении проекта {project.Name}: {ex.Message}");
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

        //// Обработчик команды "Добавить в папку ..."
        //private void AddToFolder_Click(object sender, RoutedEventArgs e)
        //{
        //    // Пока оставим заглушку
        //    MessageBox.Show("Функция 'Добавить в папку ...' пока не реализована.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        //}

        // Метод для изменения названия проекта
        private void RenameProject_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;

            ContextMenu contextMenu = menuItem.Parent as ContextMenu;
            if (contextMenu == null) return;

            Button button = contextMenu.PlacementTarget as Button;
            if (button == null) return;

            Project project = button.Tag as Project;
            if (project == null)
            {
                MessageBox.Show("Не удалось определить проект для переименования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Создание окна для ввода нового имени
            var inputDialog = new Window
            {
                Title = "Переименование проекта",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBlock = new TextBlock { Text = "Введите новое название проекта:" };
            var textBox = new TextBox { Text = project.Name, Margin = new Thickness(0, 5, 0, 10) };
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
                return;
            }

            string newProjectName = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newProjectName))
            {
                MessageBox.Show("Имя проекта не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (newProjectName.Equals("TemporaryTracks", StringComparison.OrdinalIgnoreCase) ||
                newProjectName.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Имя проекта не может быть 'TemporaryTracks' или 'Favorites'!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем, не существует ли уже проект с таким именем
            string projectsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");
            string favoritesFolder = System.IO.Path.Combine(projectsFolder, "Favorites");
            string newProjectFolderInProjects = System.IO.Path.Combine(projectsFolder, newProjectName);
            string newProjectFolderInFavorites = System.IO.Path.Combine(favoritesFolder, newProjectName);

            string currentProjectFolder = System.IO.Path.GetDirectoryName(project.ProjectPath);
            if ((Directory.Exists(newProjectFolderInProjects) && newProjectFolderInProjects != currentProjectFolder) ||
                (Directory.Exists(newProjectFolderInFavorites) && newProjectFolderInFavorites != currentProjectFolder))
            {
                MessageBox.Show("Проект с таким именем уже существует в папке Projects или Favorites!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Переименовываем папку проекта
                string newProjectFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(currentProjectFolder), newProjectName);
                Directory.Move(currentProjectFolder, newProjectFolder);
                System.Diagnostics.Debug.WriteLine($"Папка проекта переименована: {currentProjectFolder} -> {newProjectFolder}");

                // Формируем путь к файлу .daw в новой папке (с учетом старого имени файла)
                string oldProjectFileName = $"{project.Name}.daw";
                string oldProjectFileInNewFolder = System.IO.Path.Combine(newProjectFolder, oldProjectFileName);
                string newProjectFile = System.IO.Path.Combine(newProjectFolder, $"{newProjectName}.daw");

                // Переименовываем файл .daw
                if (File.Exists(oldProjectFileInNewFolder))
                {
                    File.Move(oldProjectFileInNewFolder, newProjectFile);
                    System.Diagnostics.Debug.WriteLine($"Файл проекта переименован: {oldProjectFileInNewFolder} -> {newProjectFile}");
                }
                else
                {
                    MessageBox.Show($"Файл проекта не найден в новой папке: {oldProjectFileInNewFolder}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Файл проекта не найден: {oldProjectFileInNewFolder}");
                    return; // Прерываем операцию, если файл не найден
                }

                // Обновляем свойства проекта
                project.Name = newProjectName;
                project.ProjectPath = newProjectFile;

                // Обновляем ListView
                listView_projects.ItemsSource = null;
                listView_projects.ItemsSource = projectsList;
                System.Diagnostics.Debug.WriteLine($"Проект переименован в списке: {newProjectName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переименовании проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Ошибка при переименовании проекта {project.Name}: {ex.Message}");
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
                        projectsList = projectsList.OrderBy(p => p.Name).ToList();
                        break;
                    case "По имени (Я-А, Z-A)":
                        projectsList = projectsList.OrderByDescending(p => p.Name).ToList();
                        break;
                    case "По дате создания (сначала старые)":
                        projectsList = projectsList.OrderBy(p => p.Date_time_create).ToList();
                        break;
                    case "По дате создания (сначала новые)":
                        projectsList = projectsList.OrderByDescending(p => p.Date_time_create).ToList();
                        break;
                    case "По дате изменения (сначала старые)":
                        projectsList = projectsList.OrderBy(p => p.Date_time_change).ToList();
                        break;
                    case "По дате изменения (сначала новые)":
                        projectsList = projectsList.OrderByDescending(p => p.Date_time_change).ToList();
                        break;
                }

                listView_projects.ItemsSource = null; // Очищаем старый список
                listView_projects.ItemsSource = projectsList; // Устанавливаем обновленный список
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

        private void AllProjectsButtonClick(object sender, RoutedEventArgs e)
        {
            AllButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#6ECCD7");
            FavoriteButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#76DDE8");
            listView_projects.ItemsSource = projectsList;
            TitleTextBlock.Text = "Список проектов";
            IsFavoriteProjectList = false;
        }

        private void FavoriteProjectsButtonClick(object sender, RoutedEventArgs e)
        {
            AllButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#76DDE8");
            FavoriteButtonBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#6ECCD7");
            listView_projects.ItemsSource = projectsList.Where(n => n.IsFavorite == true);
            TitleTextBlock.Text = "Список избранных проектов";
            IsFavoriteProjectList = true;
        }
    }
}