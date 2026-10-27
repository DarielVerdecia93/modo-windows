# Modo Windows

Botón flotante para cambiar entre el **modo claro** y el **modo oscuro** de Windows con un solo clic.

Es una pequeña burbuja circular, transparente y arrastrable, que se queda en una esquina de la pantalla. Al pulsarla cambia el tema de Windows y de las aplicaciones al instante, igual que en *Configuración > Personalización > Colores*.

## Características

- **Un clic** cambia entre modo claro y modo oscuro (sistema y aplicaciones).
- **Arrastrable**: se puede mover a cualquier parte de la pantalla.
- **Discreta**: semitransparente en reposo y opaca al pasar el ratón por encima.
- **Superponer a las ventanas**: opción del menú contextual para mantenerla siempre por encima del resto de ventanas, o no. La preferencia se guarda.
- **Sigue el tema del sistema**: si el tema se cambia desde Configuración u otra app, el icono se actualiza solo.
- **Idioma de Windows**: la interfaz usa el idioma de visualización de Windows (español, inglés, portugués, francés, alemán, italiano y catalán; inglés si el idioma no está traducido).
- No aparece en la barra de tareas ni en Alt+Tab, y solo puede haber una instancia abierta.
- Compatible con varios monitores y escalados DPI.

## Uso

| Acción | Resultado |
| --- | --- |
| Clic izquierdo | Cambia entre modo claro y oscuro |
| Arrastrar | Mueve la burbuja |
| Clic derecho | Menú con **Superponer a las ventanas** y **Salir** |

## Requisitos

- Windows 10 versión 2004 (compilación 19041) o posterior, o Windows 11.
- Para compilar: [SDK de .NET 10](https://dotnet.microsoft.com/download).

El ejecutable incluye el runtime de Windows App SDK (`WindowsAppSDKSelfContained`), así que no hace falta instalarlo aparte.

## Compilar y ejecutar

```powershell
git clone <url-del-repositorio>
cd modo-windows
dotnet run
```

Compilación en Release para x64:

```powershell
dotnet build -c Release -p:Platform=x64
```

El ejecutable queda en `bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\ModoWindows.exe`.

### Publicar una versión independiente

Para generar una carpeta que se pueda copiar a otro equipo sin instalar .NET:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:Platform=x64
```

Cambiar `win-x64` por `win-arm64` o `win-x86` según la arquitectura.

### Iniciar con Windows

Crear un acceso directo a `ModoWindows.exe` en la carpeta de inicio (`Win + R` → `shell:startup`).

## Cómo funciona

- **Cambio de tema**: escribe `AppsUseLightTheme` y `SystemUsesLightTheme` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` y avisa al resto de ventanas con `WM_SETTINGCHANGE` (`ImmersiveColorSet`).
- **Ventana transparente**: WinUI 3 sin fondo; el HWND usa DWM blur-behind con una región vacía y la superficie XAML un pincel transparente, de modo que solo se ve el círculo.
- **Sin marco**: se eliminan los estilos de marco de la ventana y toda ella es área cliente, para que no aparezca ningún borde alrededor del círculo.
- **Preferencias**: se guardan en `HKCU\Software\ModoWindows`.

## Estructura del proyecto

| Archivo | Contenido |
| --- | --- |
| `App.xaml(.cs)` | Arranque de la app e instancia única |
| `MainWindow.xaml(.cs)` | Burbuja, menú contextual, arrastre y subclass Win32 |
| `ThemeService.cs` | Lectura y cambio del tema de Windows |
| `SettingsService.cs` | Preferencias de la app (superponer a las ventanas) |
| `Strings.cs` | Textos de la interfaz en cada idioma |
| `TransparentBackground.cs` | Fondo transparente de la ventana |
| `NativeMethods.cs` | Llamadas a la API de Win32 / DWM |

### Añadir un idioma

Añadir una entrada en `Strings.cs` con el código ISO de dos letras del idioma:

```csharp
["nl"] = new("Overschakelen naar donkere modus", "Overschakelen naar lichte modus", "Altijd bovenaan", "Afsluiten"),
```
