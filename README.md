# LevelUp · Retos y hábitos

Aplicación de escritorio **WPF (C#, .NET 10)** que convierte objetivos personales en **misiones** con experiencia, niveles y logros. Los datos se guardan en **Firebase Realtime Database** y el acceso se hace con **Firebase Authentication** (correo y contraseña).

Proyecto de las asignaturas Desarrollo de Interfaces y Computación en la Nube.

## Equipo y reparto del trabajo

| Integrante | Parte |
|---|---|
| **Atefa** | Diseño de las vistas (XAML): ventana de acceso, menú, Inicio, Misiones, Logros y Actividad. |
| **Oleg** | Perfil: ventana de perfil con los datos del jugador y el cambio de nombre. |
| **Amores** | El resto: modelos, acceso a Firebase (REST y autenticación), reglas del juego, validaciones, lógica de las vistas, reglas de seguridad y datos de Firebase, pruebas y documentación. |

## Funciones

- **Inicio de sesión** con Firebase Authentication. El token se renueva solo y, si la sesión caduca, se vuelve al login.
- **Inicio**: nivel, barra de experiencia, retos diarios de hoy y próximas misiones.
- **Misiones**: crear, modificar, eliminar y completar misiones diarias o únicas, con búsqueda y filtros.
- **Actividad**: registrar el tiempo dedicado a cada misión y consultarlo en un calendario.
- **Logros**: insignias que se desbloquean al cumplir condiciones (misiones completadas, nivel, experiencia…).
- **Perfil**: nombre, correo, nivel, experiencia y puntos del jugador, con la opción de cambiar el nombre.
- **Sin puntos dobles**: una misión diaria solo da experiencia una vez al día y una única solo una vez. El progreso se guarda con escritura condicional (ETag) para evitar problemas de concurrencia.

## Estructura

```
LevelUp/
├── Models/      Misión, Actividad, Logro, Progreso, Usuario
├── Services/    Acceso a Firebase (REST), autenticación, datos, reglas y validaciones
├── Views/       Ventanas de acceso y de perfil, y conversor del calendario
└── MainWindow   Ventana principal (un archivo parcial por vista)
```

El acceso a la base de datos usa `HttpClient` y JSON contra la API REST: GET para consultar, POST para crear, PATCH para modificar (también actualizaciones multipath) y DELETE para eliminar.

## Cómo ejecutarlo

1. Clonar el repositorio y abrir `LevelUp/LevelUp.slnx` con Visual Studio 2026 (Windows).
2. Copiar `LevelUp/firebase.config.example.json` como `LevelUp/firebase.config.json` y poner la **API key web** del proyecto de Firebase. Este archivo no se sube al repositorio.
3. Compilar y ejecutar (F5).
4. Iniciar sesión con una de las cuentas de prueba creadas en Firebase Authentication.

## Pruebas

El proyecto `LevelUp.Tests` (xUnit, 99 pruebas) comprueba las funciones principales sin conectarse a Firebase: las respuestas de la base de datos y de Authentication se simulan.

- **Reglas de progreso**: niveles, logros y que nunca se conceden puntos dos veces por la misma misión.
- **Validaciones**: campos obligatorios, números, fechas y relaciones antes de guardar (también el nombre del perfil).
- **Autenticación**: inicio de sesión, renovación del token y sesión caducada.
- **Acceso a Firebase**: GET, POST, PATCH, DELETE, rutas `.json` con el token, escritura condicional con ETag y traducción de errores.
- **Operaciones de datos**: borrado multipath de una misión con sus actividades, reintento al completar si otra sesión cambió el progreso, y lectura y cambio del perfil.

Para ejecutarlas: en Visual Studio, menú **Prueba → Ejecutar todas las pruebas**, o desde la carpeta del repositorio:

```
dotnet test LevelUp/LevelUp.slnx
```

## Datos en Firebase

```
/catalogo/logros/{logroId}        Catálogo común de insignias (solo lectura)
/usuarios/{uid}/perfil            Nombre y correo
/usuarios/{uid}/progreso          Experiencia, nivel, puntos, misiones completadas y logros
/usuarios/{uid}/misiones/{id}     Misiones del usuario
/usuarios/{uid}/actividades/{id}  Tiempo dedicado a cada misión (misionId → misión)
```

Las reglas ([`firebase/database.rules.json`](firebase/database.rules.json)) solo permiten leer y escribir en `/usuarios/{uid}` cuando el usuario ha iniciado sesión y su `uid` coincide. El catálogo se puede leer con la sesión iniciada, pero nadie puede escribir en él. Además validan los campos esenciales: títulos, experiencia entre 1 y 500, fechas `yyyy-MM-dd`, minutos entre 1 y 1440, que una actividad apunte a una misión existente y que la experiencia del progreso nunca disminuya.

El esquema completo con los campos de cada nodo está en [`firebase/esquema.md`](firebase/esquema.md) y la exportación con datos de prueba en [`firebase/levelup-export.json`](firebase/levelup-export.json).
