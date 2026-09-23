# PokeHex Local

Editor local de archivos de guardado Pokémon (UI en español), basado en [PKHeX.Core](https://github.com/kwsch/PKHeX).

## Uso diario (Windows)

1. Genera el ejecutable:

```powershell
.\scripts\publish-win.ps1
```

2. Abre `dist\win-x64\PokeHexLocal.exe` con doble clic (o crea un acceso directo).

**Distribución:** copia la carpeta `dist\win-x64` entera (`PokeHexLocal.exe` **y** la carpeta `wwwroot`). Si mueves solo el `.exe`, la ventana no arranca.

**Requisito:** [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (incluido en la mayoría de Windows 10/11 con Edge). Si falta, la app muestra un diálogo de error al iniciar.

## Desarrollo

```powershell
dotnet restore
dotnet run --project src/PokeHex.Desktop
```

QA en navegador (opcional):

```powershell
dotnet run --project src/PokeHex.Web
```

## Compatibilidad

- **Juegos oficiales** Gen 1–9 (detección automática).
- **Extensiones de save:** `.sav`, `.srm` (RetroArch/libretro), `.dsv` (DeSmuME), `.fla`, `.sa1`/`.sa2`, `.dat`, `.gci`, `.bin`, `main`. Los savestates (p. ej. `.ss0` de mGBA) **no** son saves de partida.
- Si un `.srm` lleva relleno típico de emulador (ceros/`0xFF` al final o contenedor GBA libretro), se intenta recortar al tamaño oficial antes de detectar.
- **Hackroms:** solo best-effort. Si la estructura del save coincide con el juego base, se abre. Si no, mensaje de error y opción de «forzar como juego» (riesgo de corrupción).
- Los saves cifrados de consola no son compatibles: usa Checkpoint/JKSM u equivalente.

## Licencia

GPLv3 — ver [LICENSE](LICENSE). Incluye PKHeX.Core (GPLv3).
