
using System.Windows;
using LevelUp.Views;

namespace LevelUp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Mostrar Inicio al abrir la aplicación
            MostrarInicio();
        }

        private void MostrarInicio()
        {
           
        }

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            MostrarInicio();
        }

        private void btnMisiones_Click(object sender, RoutedEventArgs e)
        {
            ContenedorVistas.Content = new MisionesView();
            txtTituloPagina.Text = "Misiones";
        }

        private void btnLogros_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void btnActividad_Click(object sender, RoutedEventArgs e)
        {
        }

        private void btnPerfil_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
