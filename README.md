# Notitas

Aplicación de escritorio para tomar apuntes escolares en Windows. Todo es local: no hay
cuentas, ni nube, ni telemetría. Las notas, materias y ajustes viven en tu equipo.

## Funciones

- Notas con formato: títulos, negrita, cursiva, subrayado, listas, checklists y enlaces.
- Materias con color e icono; renombrado en línea, archivado y restauración.
- Guardado automático mientras escribes.
- Exportación a Word (`.docx`) sin necesidad de tener Word instalado.
- Tema claro y oscuro, con color de acento configurable (colores rápidos, RGB o HEX).
- Zoom independiente para la interfaz y para la nota abierta.
- Sección de depuración técnica: estado en ejecución, rutas, memoria, nivel de log,
  eventos recientes y copia de diagnóstico.

## Requisitos

Windows 10 o superior (64 bits). El ejecutable publicado es autocontenido: no hace falta
instalar .NET.

## Dónde se guardan los datos

```
%APPDATA%\Notitas\notitas.db      base de datos SQLite
%APPDATA%\Notitas\settings.json   preferencias
%APPDATA%\Notitas\logs\           registros
```

## Compilar desde el código

Necesitas el SDK de .NET 8.

```bash
dotnet build -c Release
```

Para generar un ejecutable único y autocontenido:

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```

El proyecto se puede compilar desde Linux gracias a `EnableWindowsTargeting`, aunque WPF
solo se ejecuta en Windows (o bajo Wine).

## Verificación automática

La aplicación incluye un modo de autocomprobación que ejercita la interfaz real, valida la
persistencia y la exportación, y guarda capturas de pantalla:

```
Notitas.exe --selftest C:\ruta\de\salida
```

Trabaja sobre una carpeta de datos propia (`selftest-data`), por lo que nunca modifica tus
notas reales. Devuelve código de salida 0 si todo pasa.

## Estructura

```
Notitas.csproj          proyecto
App.xaml(.cs)           arranque, temas y contenido de bienvenida
MainWindow.xaml(.cs)    ventana principal: materias, lista de notas y editor
ConfigWindow.xaml(.cs)  configuración y depuración técnica
SubjectDialog / LinkDialog / ArchivedSubjectsWindow
Models/                 modelos de datos
Services/               base de datos, ajustes, registro y exportador a Word
Themes/                 paletas clara y oscura, e iconos vectoriales
SelfTest.cs             modo de verificación automática
```

## Tecnología

C# · .NET 8 · WPF · SQLite (`Microsoft.Data.Sqlite`) · Open XML SDK (`DocumentFormat.OpenXml`).

El contenido de cada nota se guarda como XAML de `FlowDocument`, y se convierte a
WordprocessingML en el momento de exportar.

## Créditos

Los iconos son trazados vectoriales propios inspirados en el estilo de
[Feather](https://feathericons.com) (MIT, © Cole Bemis); algunos trazados coinciden en lo
esencial con los originales.

## Licencia

Copyright (C) 2026 addRian0-0

Notitas es software libre: puedes redistribuirlo y modificarlo bajo los términos de la
**Licencia Pública General de GNU** publicada por la Free Software Foundation, ya sea la
versión 3 de la licencia o (a tu elección) cualquier versión posterior.

Se distribuye con la esperanza de que resulte útil, pero **sin ninguna garantía**; ni
siquiera la garantía implícita de comerciabilidad o idoneidad para un propósito
determinado. Consulta la Licencia Pública General de GNU para más detalles. Deberías haber
recibido una copia junto a este programa; si no, ver <https://www.gnu.org/licenses/>.

Identificador SPDX: `GPL-3.0-or-later`. Texto completo en [LICENSE](LICENSE).

En la práctica: puedes usar, estudiar y modificar este código libremente, incluso con fines
comerciales, pero si distribuyes una versión modificada estás obligado a publicar su código
fuente bajo esta misma licencia.
