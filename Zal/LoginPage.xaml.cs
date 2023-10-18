﻿using Firebase.Auth;
using Firebase.Auth.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace Zal
{
    /// <summary>
    /// Interaction logic for LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        bool isLogin = false;
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";
            loginButton.IsEnabled = false;
            try
            {
                if (isLogin)
                {
                    await FirebaseUI.Instance.Client.SignInWithEmailAndPasswordAsync(email: Email.Text, password: Password.Password);
                }
                else
                {
                    await FirebaseUI.Instance.Client.CreateUserWithEmailAndPasswordAsync(email: Email.Text, password: Password.Password, displayName: Username.Text);

                }
            }
            catch (FirebaseAuthHttpException c)
            {
               

                ErrorText.Text = AddSpacesToSentence(c.Reason.ToString(),true);
                
            }
            loginButton.IsEnabled = true;
        }
        private string AddSpacesToSentence(string text, bool preserveAcronyms)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                        (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                         i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                        newText.Append(' ');
                newText.Append(text[i]);
            }
            return newText.ToString();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            isLogin = !isLogin;
            if (isLogin)
            {
                welcomeText.Text = "Welcome! login to continue";
                Username.Visibility = Visibility.Hidden;
                usernameIcon.Visibility = Visibility.Hidden;
                switchButton.Content = "or Sign up";
            }
            else
            {
                welcomeText.Text = "Welcome! create an Account to get started";
                Username.Visibility = Visibility.Visible;
                usernameIcon.Visibility = Visibility.Visible;
                switchButton.Content = "or Login";
            }
        }
    }
}
