using System.Globalization;
using System.Windows.Data;

namespace LevelUp.Views
{
    /// <summary>
    /// Para el calendario de actividad: devuelve true si en ese día hay alguna actividad registrada,
    /// y así el estilo del día lo pinta de otro color.
    /// </summary>
    public class DiaConActividadConverter : IValueConverter
    {
        /// <summary>Días con actividad; lo rellena la ventana principal al cargar los datos.</summary>
        public static HashSet<DateTime> Dias { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is DateTime fecha && Dias.Contains(fecha.Date);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
