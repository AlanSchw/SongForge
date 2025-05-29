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
using NAudio.Wave;
using MusicCreate.Properties;

namespace MusicCreate
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private int _originalInputDeviceIndex;
        private int _originalOutputDeviceIndex;
        private bool _originalUseAsio;
        private string _originalAsioDriverName;
        public SettingsWindow(int currentInputDeviceIndex, int currentOutputDeviceIndex, bool useAsio, string asioDriverName)
        {
            InitializeComponent();
            _originalInputDeviceIndex = currentInputDeviceIndex;
            _originalOutputDeviceIndex = currentOutputDeviceIndex;
            _originalUseAsio = useAsio;
            _originalAsioDriverName = asioDriverName;
            LoadDevices();
        }
        private void LoadDevices()
        {
            // Устройства ввода
            InputDeviceComboBox.Items.Clear();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                InputDeviceComboBox.Items.Add(capabilities.ProductName);
            }
            InputDeviceComboBox.SelectedIndex = _originalInputDeviceIndex >= 0 && _originalInputDeviceIndex < WaveIn.DeviceCount ? _originalInputDeviceIndex : 0;

            // Устройства вывода
            OutputDeviceComboBox.Items.Clear();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                OutputDeviceComboBox.Items.Add(capabilities.ProductName);
            }
            OutputDeviceComboBox.SelectedIndex = _originalOutputDeviceIndex >= 0 && _originalOutputDeviceIndex < WaveOut.DeviceCount ? _originalOutputDeviceIndex : 0;

            // ASIO драйверы
            AsioDriverComboBox.Items.Clear();
            foreach (var driverName in AsioOut.GetDriverNames())
            {
                AsioDriverComboBox.Items.Add(driverName);
            }
            AsioDriverComboBox.SelectedItem = _originalAsioDriverName;
            UseAsioCheckBox.IsChecked = _originalUseAsio;
            AsioDriverComboBox.IsEnabled = _originalUseAsio;
        }
        private void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputDeviceComboBox.SelectedIndex >= 0)
            {
                ProjectCreateWindow.SelectedInputDeviceIndex = InputDeviceComboBox.SelectedIndex;
            }
        }

        private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutputDeviceComboBox.SelectedIndex >= 0 && !(bool)UseAsioCheckBox.IsChecked)
            {
                ProjectCreateWindow.SelectedOutputDeviceIndex = OutputDeviceComboBox.SelectedIndex;
                ProjectCreateWindow.UseAsio = false;
            }
        }
        private void UseAsioCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AsioDriverComboBox.IsEnabled = true;
            if (AsioDriverComboBox.SelectedItem != null)
            {
                ProjectCreateWindow.UseAsio = true;
                ProjectCreateWindow.AsioDriverName = AsioDriverComboBox.SelectedItem.ToString();
            }
        }

        private void UseAsioCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AsioDriverComboBox.IsEnabled = false;
            ProjectCreateWindow.UseAsio = false;
            ProjectCreateWindow.AsioDriverName = null;
            if (OutputDeviceComboBox.SelectedIndex >= 0)
            {
                ProjectCreateWindow.SelectedOutputDeviceIndex = OutputDeviceComboBox.SelectedIndex;
            }
        }
        private void AsioDriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AsioDriverComboBox.SelectedItem != null)
            {
                ProjectCreateWindow.UseAsio = true;
                ProjectCreateWindow.AsioDriverName = AsioDriverComboBox.SelectedItem.ToString();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем настройки (например, в файл или Properties.Settings)
            Properties.Settings.Default.InputDeviceIndex = ProjectCreateWindow.SelectedInputDeviceIndex;
            Properties.Settings.Default.OutputDeviceIndex = ProjectCreateWindow.SelectedOutputDeviceIndex;
            Properties.Settings.Default.UseAsio = ProjectCreateWindow.UseAsio;
            Properties.Settings.Default.AsioDriverName = ProjectCreateWindow.AsioDriverName;
            Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Восстанавливаем исходные настройки
            ProjectCreateWindow.SelectedInputDeviceIndex = _originalInputDeviceIndex;
            ProjectCreateWindow.SelectedOutputDeviceIndex = _originalOutputDeviceIndex;
            ProjectCreateWindow.UseAsio = _originalUseAsio;
            ProjectCreateWindow.AsioDriverName = _originalAsioDriverName;
            DialogResult = false;
            Close();
        }
    }
}
