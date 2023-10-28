using Firebase.Auth;
using Firebase.Auth.UI;
using Firebase.Auth.UI.Pages;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Zal;
namespace Zal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            FirebaseUI.Instance.Client.AuthStateChanged += this.AuthStateChanged;

            setupTrayMenu();
            Logger.ResetLog();

        }
        
        private void AuthStateChanged(object sender, UserEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                if (e.User == null)
                {
                    this.Frame.Navigate(new LoginPage());
                }
                else if (e.User.IsAnonymous)
                {
                    this.Frame.Navigate(new LoginPage());
                }
                else if ((this.Frame.Content == null || this.Frame.Content.GetType() != typeof(MainPage)))
                {
                    this.Frame.Navigate(new MainPage());
                }
            });
        }
   
        private void setupTrayMenu()
        {
            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            ni.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            ni.Visible = true;
            var trayMenu = new ContextMenuStrip();

            // Add items to the context menu
            trayMenu.Items.Add("Open", null, (sender, e) => this.Show());
            trayMenu.Items.Add("Exit", null, (sender, e) => System.Windows.Application.Current.Shutdown());
            ni.ContextMenuStrip = trayMenu;
            ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = System.Windows.WindowState.Normal;
                };

            if(Zal.Settings.Default.minimizeToTray)
            {
                this.Hide();
            }
        }

      

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {

        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Zal.Settings.Default.minimizeToTray)
            {
                try
                {
                    Hide();
                }
                catch (Exception c) { }
            }
            else
            {
                System.Windows.Application.Current.Shutdown();
            }
           
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var process in Process.GetProcessesByName("task_manager"))
            {
                process.Kill();
            }
        }
    }
}
