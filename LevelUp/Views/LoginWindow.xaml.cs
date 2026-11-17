using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LevelUp.Views
{
    /// <summary>
    /// Lógica de interacción para LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async Task btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string correoIntroducido = txtCorreo.Text;

            var usuario = await FirebaseAuth.DefaultInstance.SignInWithEmailAndPasswordAsync(
                txtCorreo.Text, txtPassword.Password);
        }
    }
}
