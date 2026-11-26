# Esquema de la base de datos (Realtime Database)

Exportación con datos ficticios de prueba: [`levelup-export.json`](levelup-export.json).

Todas las claves son identificadores estables: los logros usan `lg01`, `lg02`…, las misiones y actividades usan la clave que genera Firebase al hacer POST y cada usuario usa su `uid` de Firebase Authentication. Los nombres visibles nunca se usan como clave.

```
/
├── catalogo/
│   └── logros/
│       └── {logroId}                  (lg01 … lg12)
│           ├── titulo: string
│           ├── descripcion: string
│           ├── icono: string
│           ├── puntos: int            puntos que da al desbloquearlo
│           └── condicion/
│               ├── tipo: string       completadas | diarias | unicas | nivel | experiencia
│               └── valor: int         cantidad necesaria
│
└── usuarios/
    └── {uid}/                         solo accesible por ese usuario
        ├── perfil/
        │   ├── nombre: string
        │   └── email: string
        │
        ├── misiones/
        │   └── {misionId}
        │       ├── titulo: string             obligatorio, máx. 60
        │       ├── descripcion: string        máx. 300
        │       ├── categoria: string          Salud, Deporte, Estudio, Trabajo, Hogar, Bienestar, Ocio, Otros
        │       ├── experiencia: int           1 – 500 XP al completarla
        │       ├── tipo: string               Diaria | Única
        │       ├── estado: string             Pendiente | En curso | Completada
        │       ├── fechaLimite: "yyyy-MM-dd"  opcional
        │       └── creada: "yyyy-MM-dd"
        │
        ├── actividades/
        │   └── {actividadId}
        │       ├── misionId: string           referencia a usuarios/{uid}/misiones/{misionId}
        │       ├── descripcion: string        obligatoria, máx. 100
        │       ├── fecha: "yyyy-MM-dd"        no puede ser futura
        │       └── minutos: int               1 – 1440
        │
        └── progreso/
            ├── experiencia: int
            ├── nivel: int                     experiencia / 100 + 1
            ├── puntos: int                    experiencia + puntos de los logros
            ├── completados/
            │   └── {misionId}/
            │       └── {clave}                fecha (diarias) o "unica" (únicas)
            │           ├── fecha: "yyyy-MM-dd"
            │           └── experiencia: int
            └── logros/
                └── {logroId}: "yyyy-MM-dd"    fecha de desbloqueo
```

## Qué guarda cada apartado

| Nodo | Contenido |
|---|---|
| `catalogo/logros` | Insignias comunes a todos los jugadores. Es de solo lectura desde la aplicación. |
| `usuarios/{uid}/perfil` | Nombre visible y correo de la cuenta. |
| `usuarios/{uid}/misiones` | Objetivos personales convertidos en misiones diarias o únicas. |
| `usuarios/{uid}/actividades` | Tiempo dedicado a cada misión en un día concreto. |
| `usuarios/{uid}/progreso` | Experiencia, nivel, puntos, registro de misiones completadas y logros desbloqueados. |

## Cómo se evita conceder puntos dos veces

`progreso/completados/{misionId}` guarda una entrada por día en las misiones diarias (clave `2026-11-24`) y una sola entrada `unica` en las misiones únicas. Antes de sumar experiencia se comprueba que esa clave no exista.

El nodo `progreso` se escribe con escritura condicional (cabecera `if-match` con el ETag leído). Si otra sesión lo ha cambiado entretanto, Firebase responde `412` y la aplicación vuelve a leerlo antes de sumar.

## Datos de prueba

| Cuenta | Misiones | Actividades | Progreso |
|---|---|---|---|
| `prueba1@levelup.com` | 6 (3 diarias, 3 únicas) | 6 | Nivel 3, 230 XP, 335 puntos, 5 logros |
| `prueba2@levelup.com` | 4 (2 diarias, 2 únicas) | 2 | Nivel 1, 10 XP, 20 puntos, 1 logro |

El catálogo tiene 12 logros.
