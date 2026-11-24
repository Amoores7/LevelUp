using System.Windows;
using System.Windows.Controls;
using LevelUp.Models;
using LevelUp.Services;

namespace LevelUp
{
    /// <summary>Vista Misiones: listado con filtros, detalle, alta, edición, borrado y completar.</summary>
    public partial class MainWindow
    {
        private Mision? MisionSeleccionada => listaMisiones.SelectedItem as Mision;

        // ------------------------------------------------------------------
        // Listado y filtros
        // ------------------------------------------------------------------

        private void MostrarMisiones()
        {
            string? idSeleccionada = MisionSeleccionada?.Id;
            string texto = txtBuscarMision.Text.Trim();
            string filtro = cmbFiltroEstado.SelectedItem as string ?? "Todas";

            var filtradas = misiones.Where(m =>
                    texto == ""
                    || m.Titulo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || m.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || m.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .Where(m => filtro switch
                {
                    "Diarias" => m.Tipo == Mision.TipoDiaria,
                    "Únicas" => m.Tipo == Mision.TipoUnica,
                    "Todas" => true,
                    _ => m.Estado == filtro
                })
                .ToList();

            listaMisiones.ItemsSource = filtradas;
            txtTotalMisionesListado.Text = $"Mostrando {filtradas.Count} de {misiones.Count} misiones";

            txtMisionesVacia.Text = misiones.Count == 0
                ? "Todavía no tienes misiones.\nPulsa «Nueva misión» en la pestaña Gestión."
                : "Ninguna misión coincide con la búsqueda o el filtro.";
            txtMisionesVacia.Visibility = filtradas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // La lista de la vista Actividad también usa las misiones
            cmbMisionActividad.ItemsSource = misiones;

            SeleccionarMision(idSeleccionada);
        }

        private void SeleccionarMision(string? id)
        {
            if (id == null)
                return;
            var mision = (listaMisiones.ItemsSource as List<Mision>)?.FirstOrDefault(m => m.Id == id);
            if (mision != null)
            {
                listaMisiones.SelectedItem = mision;
                listaMisiones.ScrollIntoView(mision);
            }
        }

        private void FiltroMisiones_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                MostrarMisiones();
        }

        private void listaMisiones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var mision = MisionSeleccionada;
            if (mision == null)
            {
                ActualizarBotonesMision();
                return;
            }

            txtTituloMision.Text = mision.Titulo;
            txtDescripcionMision.Text = mision.Descripcion;
            cmbCategoriaMision.SelectedItem = mision.Categoria;
            cmbTipoMision.SelectedItem = mision.Tipo;
            txtExperienciaMision.Text = mision.Experiencia.ToString();
            dpFechaMision.SelectedDate = mision.FechaLimite?.ToDateTime(TimeOnly.MinValue);
            cmbEstadoMision.SelectedItem = mision.Estado;

            // Una vez completada alguna vez no se puede cambiar el tipo (evita volver a ganar la XP).
            bool tieneCompletados = progreso.Completados.ContainsKey(mision.Id);
            cmbTipoMision.IsEnabled = !tieneCompletados;
            cmbEstadoMision.IsEnabled = mision.Estado != Mision.EstadoCompletada;

            txtInfoMision.Text = (mision.Tipo, mision.CompletadaAhora) switch
            {
                (Mision.TipoDiaria, true) => "✔ Reto completado hoy. Vuelve mañana para ganar más XP.",
                (Mision.TipoDiaria, false) => $"Reto diario pendiente hoy: +{mision.Experiencia} XP.",
                (_, true) => "✔ Misión completada.",
                _ => $"Al completarla ganarás {mision.Experiencia} XP."
            };

            ActualizarBotonesMision();
        }

        /// <summary>Modificar, Eliminar y Completar solo tienen sentido con una misión seleccionada.</summary>
        private void ActualizarBotonesMision()
        {
            var mision = MisionSeleccionada;
            bool haySeleccion = mision != null && !ocupado;
            btnModificarMision.IsEnabled = haySeleccion;
            btnEliminarMision.IsEnabled = haySeleccion;
            btnCompletarMision.IsEnabled = haySeleccion && !mision!.CompletadaAhora;
            btnGuardarMision.IsEnabled = !ocupado;
        }

        private void LimpiarFormularioMision()
        {
            listaMisiones.SelectedItem = null;
            txtTituloMision.Clear();
            txtDescripcionMision.Clear();
            cmbCategoriaMision.SelectedIndex = -1;
            cmbTipoMision.SelectedItem = Mision.TipoDiaria;
            txtExperienciaMision.Text = "10";
            dpFechaMision.SelectedDate = null;
            cmbEstadoMision.SelectedItem = Mision.EstadoPendiente;
            cmbTipoMision.IsEnabled = true;
            cmbEstadoMision.IsEnabled = true;
            txtInfoMision.Text = "";
            ActualizarBotonesMision();
        }

        // ------------------------------------------------------------------
        // Botones
        // ------------------------------------------------------------------

        private void btnNuevoMision_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormularioMision();
            txtInfoMision.Text = "Nueva misión: rellena los datos y pulsa «Guardar misión».";
            txtTituloMision.Focus();
            MostrarEstado("Rellena los datos de la nueva misión.", TipoEstado.Info);
        }

        private void btnLimpiarMision_Click(object sender, RoutedEventArgs e) => LimpiarFormularioMision();

        /// <summary>Lee y valida el formulario. Devuelve null si hay errores (ya mostrados).</summary>
        private Mision? LeerFormularioMision(Mision? original)
        {
            DateOnly? fechaLimite = dpFechaMision.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null;

            var errores = Validaciones.ValidarMision(
                txtTituloMision.Text, txtDescripcionMision.Text,
                cmbCategoriaMision.SelectedItem as string, txtExperienciaMision.Text,
                cmbTipoMision.SelectedItem as string, cmbEstadoMision.SelectedItem as string,
                fechaLimite, original?.Estado, Hoy, out int experiencia);

            if (original != null && original.Tipo != cmbTipoMision.SelectedItem as string
                && progreso.Completados.ContainsKey(original.Id))
                errores.Add("No se puede cambiar el tipo de una misión que ya se ha completado alguna vez.");

            if (MostrarErroresValidacion(errores))
                return null;

            return new Mision
            {
                Id = original?.Id ?? "",
                Titulo = txtTituloMision.Text.Trim(),
                Descripcion = txtDescripcionMision.Text.Trim(),
                Categoria = (string)cmbCategoriaMision.SelectedItem,
                Experiencia = experiencia,
                Tipo = (string)cmbTipoMision.SelectedItem,
                Estado = (string)cmbEstadoMision.SelectedItem,
                FechaLimite = fechaLimite,
                Creada = original?.Creada is { Length: > 0 } creada ? creada : Hoy.ToString("yyyy-MM-dd")
            };
        }

        private async void btnGuardarMision_Click(object sender, RoutedEventArgs e)
        {
            if (MisionSeleccionada != null)
            {
                MessageBox.Show(this,
                    "Tienes seleccionada una misión existente. Para cambiarla usa «Modificar misión»; " +
                    "para crear otra pulsa antes «Nueva misión».",
                    "Guardar misión", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var nueva = LeerFormularioMision(null);
            if (nueva == null)
                return;

            if (await EjecutarAsync("Guardando misión…", async () =>
                {
                    await sesion.Datos.CrearMisionAsync(nueva); // POST: nueva.Id = clave generada
                    await RecargarDatosAsync();
                }, $"Misión «{nueva.Titulo}» creada."))
            {
                SeleccionarMision(nueva.Id);
            }
        }

        private async void btnModificarMision_Click(object sender, RoutedEventArgs e)
        {
            var original = MisionSeleccionada;
            if (original == null)
                return;

            var modificada = LeerFormularioMision(original);
            if (modificada == null)
                return;

            if (await EjecutarAsync("Modificando misión…", async () =>
                {
                    await sesion.Datos.ModificarMisionAsync(modificada); // PATCH
                    await RecargarDatosAsync();
                }, $"Misión «{modificada.Titulo}» modificada."))
            {
                SeleccionarMision(modificada.Id);
            }
        }

        private async void btnEliminarMision_Click(object sender, RoutedEventArgs e)
        {
            var mision = MisionSeleccionada;
            if (mision == null)
                return;

            int numActividades = actividades.Count(a => a.MisionId == mision.Id);
            string aviso = $"¿Seguro que quieres eliminar la misión «{mision.Titulo}»?";
            if (numActividades > 0)
                aviso += $"\n\nTambién se eliminarán sus {numActividades} actividades registradas.";
            aviso += "\n\nLa experiencia que ya ganaste con ella se conserva.";

            if (MessageBox.Show(this, aviso, "Confirmar borrado",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            if (await EjecutarAsync("Eliminando misión…", async () =>
                {
                    await sesion.Datos.EliminarMisionAsync(mision.Id); // PATCH multipath
                    await RecargarDatosAsync();
                }, $"Misión «{mision.Titulo}» eliminada."))
            {
                LimpiarFormularioMision();
            }
        }

        private async void btnCompletarMision_Click(object sender, RoutedEventArgs e)
        {
            var mision = MisionSeleccionada;
            if (mision == null)
                return;

            ResultadoCompletar? resultado = null;

            bool correcto = await EjecutarAsync("Completando misión…", async () =>
            {
                resultado = await sesion.Datos.CompletarMisionAsync(mision, catalogoLogros, Hoy);
                await RecargarDatosAsync();
            });

            if (!correcto || resultado == null)
                return;

            SeleccionarMision(mision.Id);

            if (!resultado.Completada)
            {
                string yaHecha = mision.Tipo == Mision.TipoDiaria
                    ? "Este reto ya lo completaste hoy. No se suma experiencia otra vez."
                    : "Esta misión ya estaba completada. No se suma experiencia otra vez.";
                MostrarEstado(yaHecha, TipoEstado.Info);
                MessageBox.Show(this, yaHecha, "Misión ya completada", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string mensaje = $"¡Misión completada! +{resultado.ExperienciaGanada} XP.";
            if (resultado.SubioDeNivel)
                mensaje += $"\n\n⬆ ¡Has subido al nivel {resultado.Progreso.Nivel}!";
            if (resultado.LogrosNuevos.Count > 0)
                mensaje += "\n\nLogros desbloqueados:\n" + string.Join("\n",
                    resultado.LogrosNuevos.Select(l => $"  {l.Icono} {l.Titulo} (+{l.Puntos} puntos)"));

            MostrarEstado($"Misión «{mision.Titulo}» completada: +{resultado.ExperienciaGanada} XP.", TipoEstado.Correcto);
            MessageBox.Show(this, mensaje, "LevelUp", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
