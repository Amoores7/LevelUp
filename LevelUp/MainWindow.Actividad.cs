using System.Windows;
using System.Windows.Controls;
using LevelUp.Models;
using LevelUp.Services;
using LevelUp.Views;

namespace LevelUp
{
    /// <summary>Vista Actividad: calendario de actividad, listado, alta, edición y borrado.</summary>
    public partial class MainWindow
    {
        private Actividad? ActividadSeleccionada => listaActividades.SelectedItem as Actividad;

        // ------------------------------------------------------------------
        // Listado, filtros y calendario
        // ------------------------------------------------------------------

        private void MostrarActividades()
        {
            string? idSeleccionada = ActividadSeleccionada?.Id;
            string texto = txtBuscarActividad.Text.Trim();
            DateOnly? dia = calendarioActividad.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null;

            var filtradas = actividades
                .Where(a => texto == ""
                            || a.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase)
                            || a.Mision.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .Where(a => dia == null || a.Fecha == dia)
                .ToList();

            listaActividades.ItemsSource = filtradas;

            int minutos = filtradas.Sum(a => a.Minutos);
            txtTotalActividades.Text =
                $"Mostrando {filtradas.Count} de {actividades.Count} actividades  ·  Tiempo total: {minutos / 60} h {minutos % 60:00} min";

            txtActividadesVacia.Text = actividades.Count == 0
                ? "Todavía no has registrado actividad.\nPulsa «Nueva actividad»."
                : dia != null
                    ? $"No hay actividad el {dia:dd/MM/yyyy}."
                    : "Ninguna actividad coincide con la búsqueda.";
            txtActividadesVacia.Visibility = filtradas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            txtResumenDia.Text = dia == null
                ? ""
                : $"{dia:dd/MM/yyyy}: {filtradas.Count} actividades, {minutos} min";

            RefrescarCalendario();
            SeleccionarActividad(idSeleccionada);
        }

        /// <summary>Actualiza qué días se marcan en el calendario.</summary>
        private void RefrescarCalendario()
        {
            DiaConActividadConverter.Dias.Clear();
            foreach (var actividad in actividades)
                DiaConActividadConverter.Dias.Add(actividad.Fecha.ToDateTime(TimeOnly.MinValue));

            // Cambiar de mes y volver obliga al calendario a repintar los días.
            var mes = calendarioActividad.DisplayDate;
            calendarioActividad.DisplayDate = mes.AddMonths(1);
            calendarioActividad.DisplayDate = mes;
        }

        private void SeleccionarActividad(string? id)
        {
            if (id == null)
                return;
            var actividad = (listaActividades.ItemsSource as List<Actividad>)?.FirstOrDefault(a => a.Id == id);
            if (actividad != null)
            {
                listaActividades.SelectedItem = actividad;
                listaActividades.ScrollIntoView(actividad);
            }
        }

        private void FiltroActividades_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                MostrarActividades();
        }

        private void calendarioActividad_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            MostrarActividades();

            // Si no hay ninguna actividad seleccionada, la fecha del formulario sigue al calendario.
            if (ActividadSeleccionada == null && calendarioActividad.SelectedDate is DateTime fecha)
                dpFechaActividad.SelectedDate = fecha;

            // El calendario captura el ratón al hacer clic; se libera para que el primer clic fuera funcione.
            System.Windows.Input.Mouse.Capture(null);
        }

        private void btnTodasFechas_Click(object sender, RoutedEventArgs e)
        {
            calendarioActividad.SelectedDate = null;
            MostrarActividades();
        }

        private void listaActividades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var actividad = ActividadSeleccionada;
            if (actividad != null)
            {
                txtDescripcionActividad.Text = actividad.Descripcion;
                cmbMisionActividad.SelectedItem = misiones.FirstOrDefault(m => m.Id == actividad.MisionId);
                dpFechaActividad.SelectedDate = actividad.Fecha.ToDateTime(TimeOnly.MinValue);
                txtTiempoActividad.Text = actividad.Minutos.ToString();
            }
            ActualizarBotonesActividad();
        }

        private void ActualizarBotonesActividad()
        {
            bool haySeleccion = ActividadSeleccionada != null && !ocupado;
            btnModificarActividad.IsEnabled = haySeleccion;
            btnEliminarActividad.IsEnabled = haySeleccion;
            btnGuardarActividad.IsEnabled = !ocupado;
        }

        private void LimpiarFormularioActividad()
        {
            listaActividades.SelectedItem = null;
            txtDescripcionActividad.Clear();
            cmbMisionActividad.SelectedIndex = -1;
            dpFechaActividad.SelectedDate = calendarioActividad.SelectedDate ?? DateTime.Today;
            txtTiempoActividad.Clear();
            ActualizarBotonesActividad();
        }

        // ------------------------------------------------------------------
        // Botones
        // ------------------------------------------------------------------

        private void btnNuevaActividad_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormularioActividad();
            txtDescripcionActividad.Focus();
            MostrarEstado("Rellena los datos de la nueva actividad.", TipoEstado.Info);
        }

        private void btnLimpiarActividad_Click(object sender, RoutedEventArgs e) => LimpiarFormularioActividad();

        private Actividad? LeerFormularioActividad(Actividad? original)
        {
            var mision = cmbMisionActividad.SelectedItem as Mision;
            DateOnly? fecha = dpFechaActividad.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null;

            var errores = Validaciones.ValidarActividad(
                txtDescripcionActividad.Text, mision, misiones.Select(m => m.Id),
                fecha, txtTiempoActividad.Text, Hoy, out int minutos);

            if (MostrarErroresValidacion(errores))
                return null;

            return new Actividad
            {
                Id = original?.Id ?? "",
                MisionId = mision!.Id,
                Descripcion = txtDescripcionActividad.Text.Trim(),
                Fecha = fecha!.Value,
                Minutos = minutos
            };
        }

        private async void btnGuardarActividad_Click(object sender, RoutedEventArgs e)
        {
            if (ActividadSeleccionada != null)
            {
                MessageBox.Show(this,
                    "Tienes seleccionada una actividad existente. Para cambiarla usa «Modificar actividad»; " +
                    "para registrar otra pulsa antes «Nueva actividad».",
                    "Guardar actividad", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var nueva = LeerFormularioActividad(null);
            if (nueva == null)
                return;

            if (await EjecutarAsync("Guardando actividad…", async () =>
                {
                    await sesion.Datos.CrearActividadAsync(nueva); // POST
                    await RecargarDatosAsync();
                }, "Actividad registrada."))
            {
                SeleccionarActividad(nueva.Id);
            }
        }

        private async void btnModificarActividad_Click(object sender, RoutedEventArgs e)
        {
            var original = ActividadSeleccionada;
            if (original == null)
                return;

            var modificada = LeerFormularioActividad(original);
            if (modificada == null)
                return;

            if (await EjecutarAsync("Modificando actividad…", async () =>
                {
                    await sesion.Datos.ModificarActividadAsync(modificada); // PATCH
                    await RecargarDatosAsync();
                }, "Actividad modificada."))
            {
                SeleccionarActividad(modificada.Id);
            }
        }

        private async void btnEliminarActividad_Click(object sender, RoutedEventArgs e)
        {
            var actividad = ActividadSeleccionada;
            if (actividad == null)
                return;

            if (MessageBox.Show(this,
                    $"¿Seguro que quieres eliminar la actividad «{actividad.Descripcion}» del {actividad.FechaTexto}?",
                    "Confirmar borrado", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            if (await EjecutarAsync("Eliminando actividad…", async () =>
                {
                    await sesion.Datos.EliminarActividadAsync(actividad.Id); // DELETE
                    await RecargarDatosAsync();
                }, "Actividad eliminada."))
            {
                LimpiarFormularioActividad();
            }
        }
    }
}
