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

namespace MusicCreate
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            main_frame.NavigationService.Navigate(new Pages.ProjectsListPage(this));
            ProjectsBtn.BorderThickness = new Thickness(0, 0, 0, 8);
            SongsBtn.BorderThickness = new Thickness(0, 0, 0, 4);
        }

        private void Menu_Projects(object sender, RoutedEventArgs e)
        {
            main_frame.NavigationService.Navigate(new Pages.ProjectsListPage(this));
            ProjectsBtn.BorderThickness = new Thickness(0, 0, 0, 8);
            SongsBtn.BorderThickness = new Thickness(0, 0, 0, 4);
        }

        private void Menu_Songs(object sender, RoutedEventArgs e)
        {
            main_frame.NavigationService.Navigate(new Pages.SongsListPage());
            ProjectsBtn.BorderThickness = new Thickness(0, 0, 0, 4);
            SongsBtn.BorderThickness = new Thickness(0, 0, 0, 8);
        }
    }
}
