using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Photino.Blazor;
using PokeHex.Services;
using PokeHex.UI;

namespace PokeHex.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            var wwwRoot = ResolveWwwRoot();
            if (wwwRoot is null)
            {
                ShowError(
                    "No se encontró la carpeta wwwroot junto al ejecutable.\n\n" +
                    "Vuelve a publicar con scripts\\publish-win.ps1 y no muevas solo el .exe.");
                return;
            }

            var builder = PhotinoBlazorAppBuilder.CreateDefault(new PhysicalFileProvider(wwwRoot), args);
            builder.Services.AddLogging();
            builder.Services.AddPokeHexServices();
            builder.Services.AddSingleton<INativeFilePicker, PhotinoFilePicker>();
            builder.RootComponents.Add<AppShell>("#app");

            var app = builder.Build();
            var picker = (PhotinoFilePicker)app.Services.GetRequiredService<INativeFilePicker>();
            picker.Attach(app);

            var window = app.MainWindow
                .SetTitle("PokeHex Local")
                .SetUseOsDefaultSize(false)
                .SetSize(1280, 800)
                .Center();

            var iconPath = ResolveAppIcon();
            if (iconPath is not null)
                window.SetIconFile(iconPath);

            AppDomain.CurrentDomain.UnhandledException += (_, error) =>
            {
                ShowError(error.ExceptionObject?.ToString() ?? "Error desconocido");
            };

            app.Run();
        }
        catch (Exception ex)
        {
            ShowError(
                "No se pudo iniciar PokeHex Local.\n\n" +
                ex.Message +
                "\n\nRequisito: WebView2 Runtime (Microsoft Edge).\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/");
        }
    }

    /// <summary>
    /// Photino defaults to AppContext.BaseDirectory/wwwroot, which for PublishSingleFile
    /// is the extract temp folder — not the folder next to the .exe where publish places wwwroot.
    /// </summary>
    private static string? ResolveWwwRoot()
    {
        foreach (var candidate in WwwRootCandidates())
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
                return candidate;
        }

        return null;
    }

    private static string? ResolveAppIcon()
    {
        foreach (var dir in IconSearchDirs())
        {
            foreach (var relative in new[] { "app.ico", Path.Combine("Assets", "app.ico") })
            {
                var path = Path.Combine(dir, relative);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> IconSearchDirs()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exeDir))
            yield return exeDir;

        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static IEnumerable<string> WwwRootCandidates()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exeDir))
            yield return Path.Combine(exeDir, "wwwroot");

        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    private static void ShowError(string message)
    {
        MessageBoxW(IntPtr.Zero, message, "PokeHex Local", 0x00000010); // MB_ICONERROR
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}

public sealed class PhotinoFilePicker : INativeFilePicker
{
    private PhotinoBlazorApp? _app;
    public bool SupportsNativeDialogs => true;

    public void Attach(PhotinoBlazorApp app) => _app = app;

    public Task<string?> PickOpenAsync(string filterDescription, string[] extensions)
    {
        if (_app is null) return Task.FromResult<string?>(null);
        try
        {
            var patterns = extensions.Select(e => e.StartsWith('.') ? $"*{e}" : (e.Contains('*') ? e : $"*{e}")).ToArray();
            var paths = _app.MainWindow.ShowOpenFile(
                "Abrir save",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                false,
                [(filterDescription, patterns)]);
            return Task.FromResult(paths is { Length: > 0 } ? paths[0] : null);
        }
        catch
        {
            return Task.FromResult(NativeOpenFallback(filterDescription));
        }
    }

    public Task<string?> PickSaveAsync(string filterDescription, string[] extensions, string defaultFileName)
    {
        if (_app is null) return Task.FromResult<string?>(null);
        try
        {
            var patterns = extensions.Select(e => e.StartsWith('.') ? $"*{e}" : (e.Contains('*') ? e : $"*{e}")).ToArray();
            var path = _app.MainWindow.ShowSaveFile(
                "Guardar save",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), defaultFileName),
                [(filterDescription, patterns)]);
            return Task.FromResult(string.IsNullOrWhiteSpace(path) ? null : path);
        }
        catch
        {
            return Task.FromResult(NativeSaveFallback(filterDescription, defaultFileName));
        }
    }

    private static string? NativeOpenFallback(string filterDescription)
    {
        var filter = $"{filterDescription}\0{SaveFileExtensions.Win32OpenFilter}\0All\0*.*\0\0";
        var ofn = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            lpstrFilter = filter,
            lpstrFile = Marshal.StringToHGlobalUni(new string('\0', 260)),
            nMaxFile = 260,
            Flags = 0x00001000 | 0x00000800 // OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST
        };
        try
        {
            if (!GetOpenFileName(ref ofn)) return null;
            return Marshal.PtrToStringUni(ofn.lpstrFile);
        }
        finally
        {
            if (ofn.lpstrFile != IntPtr.Zero) Marshal.FreeHGlobal(ofn.lpstrFile);
        }
    }

    private static string? NativeSaveFallback(string filterDescription, string defaultName)
    {
        var filter = $"{filterDescription}\0*.sav;*.*\0All\0*.*\0\0";
        var ofn = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            lpstrFilter = filter,
            lpstrFile = Marshal.StringToHGlobalUni(defaultName.PadRight(260, '\0')),
            nMaxFile = 260,
            Flags = 0x00000002 // OFN_OVERWRITEPROMPT
        };
        try
        {
            if (!GetSaveFileName(ref ofn)) return null;
            return Marshal.PtrToStringUni(ofn.lpstrFile);
        }
        finally
        {
            if (ofn.lpstrFile != IntPtr.Zero) Marshal.FreeHGlobal(ofn.lpstrFile);
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
