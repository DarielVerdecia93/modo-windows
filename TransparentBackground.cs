using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using WinRT;

namespace ModoWindows;

/// <summary>
/// Hace transparente el fondo de una ventana WinUI 3 para poder dibujar
/// formas no rectangulares (aquí, un círculo).
/// La ventana tiene dos capas de fondo:
///  1. El fondo Win32 del HWND: se elimina con DWM blur-behind (región vacía)
///     y pintando WM_PAINT en negro (negro = alfa 0 para DWM).
///  2. El fondo de la superficie XAML: se elimina asignando un pincel
///     transparente vía ICompositionSupportsSystemBackdrop.
/// </summary>
internal static class TransparentBackground
{
    private static Windows.System.DispatcherQueueController? s_queueController;
    private static Windows.UI.Composition.Compositor? s_compositor;

    public static void Apply(Window window, nint hwnd)
    {
        nint emptyRegion = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var blur = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DWM_BB_ENABLE | NativeMethods.DWM_BB_BLURREGION,
                fEnable = 1,
                hRgnBlur = emptyRegion,
            };
            Marshal.ThrowExceptionForHR(NativeMethods.DwmEnableBlurBehindWindow(hwnd, blur));
        }
        finally
        {
            NativeMethods.DeleteObject(emptyRegion);
        }

        var backdropHolder = window.As<ICompositionSupportsSystemBackdrop>();
        backdropHolder.SystemBackdrop = GetSystemCompositor().CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    // El pincel de ICompositionSupportsSystemBackdrop es de Windows.UI.Composition (no Microsoft.UI),
    // que necesita un Windows.System.DispatcherQueue en el hilo de UI.
    private static Windows.UI.Composition.Compositor GetSystemCompositor()
    {
        if (s_compositor is not null)
        {
            return s_compositor;
        }

        if (Windows.System.DispatcherQueue.GetForCurrentThread() is null)
        {
            var options = new NativeMethods.DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<NativeMethods.DispatcherQueueOptions>(),
                threadType = NativeMethods.DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                apartmentType = NativeMethods.DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
            };
            Marshal.ThrowExceptionForHR(NativeMethods.CreateDispatcherQueueController(options, out nint controller));
            s_queueController = Windows.System.DispatcherQueueController.FromAbi(controller);
            Marshal.Release(controller);
        }

        s_compositor = new Windows.UI.Composition.Compositor();
        return s_compositor;
    }
}
