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

namespace MusicCreate
{
    /// <summary>
    /// Логика взаимодействия для InstrumentsSelectWindow.xaml
    /// </summary>
    public partial class InstrumentsSelectWindow : Window
    {
        public string instrumentName = "";
        public string instrumentImagePath = "";

        public Project project;
        public InstrumentsSelectWindow(Project project)
        {
            InitializeComponent();
            List<Instrument> instruments = new List<Instrument>()
            {
                new Instrument("Recording","/Images/Instruments/icon-mic.png"),
                new Instrument("Synthesizer","/Images/Instruments/icon-synthesizer.png"),
                new Instrument("Drums","/Images/Instruments/icon-drum-set.png"),
                new Instrument("Electric guitar","/Images/Instruments/icon-electric-guitar.png"),
                new Instrument("Acoustic guitar","/Images/Instruments/icon-guitar.png"),
                new Instrument("Bass","/Images/Instruments/icon-bass.png")
            };
            ListView1.ItemsSource = instruments.ToList();

            this.project = project;
        }
        public class Instrument { 
            public string Name { get; set; }
            public string ImagePath { get; set; }

            public Instrument(string name, string imagePath)
            { 
                Name = name;
                ImagePath = imagePath;
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex == 0)
            {
                instrumentName = "Recording";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-mic.png";
            }
            else if (ListView1.SelectedIndex == 1)
            {
                instrumentName = "Synthesizer";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-synthesizer.png";
            }
            else if(ListView1.SelectedIndex == 2)
            {
                instrumentName = "Drums";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-drum-set.png";
            }
            else if (ListView1.SelectedIndex == 3)
            {
                instrumentName = "Electric guitar";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-electric-guitar.png";
            }
            else if (ListView1.SelectedIndex == 4)
            {
                instrumentName = "Acoustic guitar";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-guitar.png";
            }
            else if (ListView1.SelectedIndex == 5)
            {
                instrumentName = "Bass";
                instrumentImagePath = "pack://application:,,,/Images/Instruments/icon-bass.png";
            }

            if (ListView1.SelectedIndex > 1)
            {
                // Открытие окна с инструментом
                MessageBox.Show("Открытие окна с инструментом\nПока в разработке");
            }
            if (ListView1.SelectedIndex == 1)
            {
                PlaySynthesizerWindow playSynthesizerWindow = new PlaySynthesizerWindow(project);
                playSynthesizerWindow.Show();
            }
            Close();
        }
    }
}
