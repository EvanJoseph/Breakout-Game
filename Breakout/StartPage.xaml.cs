using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Breakout
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StartPage : Page
    {

        public struct MainArgs
        {
            public bool hard_mode_on;
            public int item_prob_mult;
        }

        public StartPage()
        {
            this.InitializeComponent();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void NewGame_click(object sender, RoutedEventArgs e)
        {
            MainArgs m = new MainArgs() { hard_mode_on = false, item_prob_mult = ItemProbSelect.SelectedIndex + 1 };
            Frame.Navigate(typeof(MainPage), m);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            GameOverText.Text = (string)e.Parameter;
        }

        private void HardMode_click(object sender, RoutedEventArgs e)
        {
            MainArgs m = new MainArgs() { hard_mode_on = true, item_prob_mult = ItemProbSelect.SelectedIndex + 1 };
            Frame.Navigate(typeof(MainPage), 1);
        }
    }
}