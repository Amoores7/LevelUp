# LevelUp · Retos y hábitos

Aplicación de escritorio **WPF (C#, .NET 10)** que convierte objetivos personales en **misiones** con experiencia, niveles y logros. Los datos se guardan en **Firebase Realtime Database** y el acceso se hace con **Firebase Authentication** (correo y contraseña).

Proyecto de las asignaturas Desarrollo de Interfaces y Computación en la Nube. Equipo: Atefa, Amores y Oleg.

## Funciones

- **Inicio de sesión** con Firebase Authentication. El token se renueva solo y, si la sesión caduca, se vuelve al login.
- **Inicio**: nivel, barra de experiencia, retos diarios de hoy y próximas misiones.
- **Misiones**: crear, modificar, eliminar y completar misiones diarias o únicas, con búsqueda y filtros.
- **Actividad**: registrar el tiempo dedicado a cada misión y consultarlo en un calendario.
- **Logros**: insignias que se desbloquean al cumplir condiciones (misiones completadas, nivel, experiencia…).
- **Sin puntos dobles**: una misión diaria solo da experiencia una vez al día y una única solo una vez. El progreso se guarda con escritura condicional (ETag) para evitar problemas de concurrencia.

## Estructura

```
LevelUp/
├── Models/      Misión, Actividad, Logro, Progreso, Usuario
├── Services/    Acceso a Firebase (REST), autenticación, datos, reglas y validaciones
├── Views/       Ventana de acceso y elementos de las vistas
└── MainWindow   Ventana principal (un archivo parcial por vista)
```

El acceso a la base de datos usa `HttpClient` y JSON contra la API REST: GET para consultar, POST para crear, PATCH para modificar (también actualizaciones multipath) y DELETE para eliminar.

## Cómo ejecutarlo

1. Clonar el repositorio y abrir `LevelUp/LevelUp.slnx` con Visual Studio 2026 (Windows).
2. Copiar `LevelUp/firebase.config.example.json` como `LevelUp/firebase.config.json` y poner la **API key web** del proyecto de Firebase. Este archivo no se sube al repositorio.
3. Compilar y ejecutar (F5).
4. Iniciar sesión con una de las cuentas de prueba creadas en Firebase Authentication.

## Datos en Firebase

```
/catalogo/logros/{logroId}        Catálogo común de insignias (solo lectura)
/usuarios/{uid}/perfil            Nombre y correo
/usuarios/{uid}/progreso          Experiencia, nivel, puntos, misiones completadas y logros
/usuarios/{uid}/misiones/{id}     Misiones del usuario
/usuarios/{uid}/actividades/{id}  Tiempo dedicado a cada misión (misionId → misión)
```

Las reglas solo permiten leer y escribir en `/usuarios/{uid}` cuando el usuario ha iniciado sesión y su `uid` coincide.
