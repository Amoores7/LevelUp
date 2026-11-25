using System.Windows;
using System.Windows.Controls;

namespace LevelUp
{
    /// <summary>Vista Logros: insignias del catálogo y cuáles ha desbloqueado el usuario.</summary>
    public partial class MainWindow
    {
        private void MostrarLogros()
        {
            string filtro = cmbFiltroLogros.SelectedItem as string ?? "Todos";

            var filtrados = catalogoLogros
                .Where(l => filtro switch
                {
                    "Desbloqueados" => l.Desbloqueado,
                    "Bloqueados" => !l.Desbloqueado,
                    _ => true
                })
                .OrderByDescending(l => l.Desbloqueado)
                .ThenBy(l => l.Id)
                .ToList();

            listaLogros.ItemsSource = filtrados;

            int desbloqueados = catalogoLogros.Count(l => l.Desbloqueado);
            txtTotalLogrosListado.Text =
                $"{desbloqueados} de {catalogoLogros.Count} logros desbloqueados  ·  Mostrando {filtrados.Count}";

            txtLogrosVacio.Text = catalogoLogros.Count == 0
                ? "No se ha encontrado el catálogo de logros en Firebase."
                : filtro == "Desbloqueados"
                    ? "Aún no has desbloqueado ningún logro. ¡Completa misiones!"
                    : "No hay logros en esta categoría.";
            txtLogrosVacio.Visibility = filtrados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cmbFiltroLogros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                MostrarLogros();
        }
    }
}
