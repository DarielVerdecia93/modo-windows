using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace ModoWindows;

public sealed unsafe partial class MainWindow : Window
{
    // Círculo de 40x40 (el CornerRadius del XAML es la mitad: 20)
    private const int BubbleSizeDip = 40;
    private const int ScreenMarginDip = 24;

    // Opacidad cuando el ratón no está encima
    private const double IdleOpacity = 0.5;
    private const int DragThresholdPx = 4;
    private const nuint SubclassId = 1;

    private const uint FrameStyles =
        NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_SYSMENU |
        NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX;

    private const string SunGlyph = "\uE706";
    private const string MoonGlyph = "\uE708";

    // Solo hay una ventana; el subclass proc (estático, sin GC) la necesita.
    private static MainWindow? s_instance;

    private readonly nint _hwnd;
    private OverlappedPresenter? _presenter;
    private bool? _isLight;

    // Estado del arrastre
    private bool _pointerDown;
    private bool _dragging;
    private bool _suppressClick;
    private NativeMethods.POINT _dragStartCursor;
    private PointInt32 _dragStartWindow;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        s_instance = this;

        // El subclass va primero para que WM_GETMINMAXINFO ya aplique al redimensionar.
        NativeMethods.SetWindowSubclass(_hwnd, &SubclassProc, SubclassId, 0);
        ConfigureWindow();
        TransparentBackground.Apply(this, _hwnd);

        // Button marca los eventos de puntero como "handled"; los escuchamos igualmente para arrastrar.
        ToggleButton.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        ToggleButton.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        ToggleButton.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        ToggleButton.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerReleased), true);

        AlwaysOnTopItem.Text = Strings.AlwaysOnTop;
        AlwaysOnTopItem.IsChecked = _presenter!.IsAlwaysOnTop;
        StartWithWindowsItem.Text = Strings.StartWithWindows;
        StartWithWindowsItem.IsChecked = SettingsService.StartWithWindows;
        ExitItem.Text = Strings.Exit;

        ApplyTheme(ThemeService.IsLightTheme(), animate: false);
        RootGrid.Opacity = IdleOpacity;
    }

    private void ConfigureWindow()
    {
        _presenter = OverlappedPresenter.Create();
        _presenter.IsAlwaysOnTop = SettingsService.AlwaysOnTop;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = false;
        _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        AppWindow.SetPresenter(_presenter);

        // Aun sin borde ni barra de título el presenter deja estilos de marco; ese marco no
        // cliente es el rectángulo blanco alrededor del círculo. Se quitan todos (el subclass
        // impide en WM_STYLECHANGING que vuelvan) y WM_NCCALCSIZE hace que toda la ventana
        // sea área cliente.
        uint style = (uint)NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE);
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE, (nint)(style & ~FrameStyles));

        uint exStyle = (uint)NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~(NativeMethods.WS_EX_WINDOWEDGE | NativeMethods.WS_EX_CLIENTEDGE |
                     NativeMethods.WS_EX_DLGMODALFRAME | NativeMethods.WS_EX_STATICEDGE);
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, (nint)exStyle);

        NativeMethods.SetWindowPos(_hwnd, 0, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

        // Fuera de la barra de tareas y de Alt+Tab
        AppWindow.IsShownInSwitchers = false;

        // Sin esquinas redondeadas ni borde de color de DWM: el marco (blanco al tener foco
        // en Windows 11) desaparece al fijar el color del borde a "none". La forma la da el XAML.
        int donotround = NativeMethods.DWMWCP_DONOTROUND;
        int cornerHr = NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref donotround, sizeof(int));
        int none = NativeMethods.DWMWA_COLOR_NONE;
        int borderHr = NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref none, sizeof(int));
        System.Diagnostics.Debug.WriteLine($"[ModoWindows] corner HR=0x{cornerHr:X8} border HR=0x{borderHr:X8}");

        // Posición inicial: esquina inferior derecha del área de trabajo (encima de la barra de tareas)
        int size = GetBubbleSizePx();
        int margin = Scale(ScreenMarginDip);
        RectInt32 workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + workArea.Width - size - margin,
            workArea.Y + workArea.Height - size - margin,
            size,
            size));
    }

    private int Scale(int dip) => (int)Math.Round(dip * NativeMethods.GetDpiForWindow(_hwnd) / 96.0);

    private int GetBubbleSizePx() => Scale(BubbleSizeDip);

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressClick)
        {
            _suppressClick = false;
            return;
        }

        bool light = !ThemeService.IsLightTheme();
        ThemeService.SetLightTheme(light);
        ApplyTheme(light, animate: true);
    }

    private void AlwaysOnTopItem_Click(object sender, RoutedEventArgs e)
    {
        bool onTop = AlwaysOnTopItem.IsChecked;
        _presenter!.IsAlwaysOnTop = onTop;
        SettingsService.AlwaysOnTop = onTop;
    }

    private void StartWithWindowsItem_Click(object sender, RoutedEventArgs e) =>
        SettingsService.StartWithWindows = StartWithWindowsItem.IsChecked;

    private void ExitItem_Click(object sender, RoutedEventArgs e) => Close();

    // Opaco con el ratón encima, semitransparente el resto del tiempo (la transición la anima OpacityTransition).
    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e) => RootGrid.Opacity = 1.0;

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            RootGrid.Opacity = IdleOpacity;
        }
    }

    private void ApplyTheme(bool light, bool animate)
    {
        if (_isLight == light)
        {
            return;
        }

        _isLight = light;
        RootGrid.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        ThemeIcon.Glyph = light ? SunGlyph : MoonGlyph;

        string action = light ? Strings.SwitchToDark : Strings.SwitchToLight;
        ToolTipService.SetToolTip(ToggleButton, action);
        AutomationProperties.SetName(ToggleButton, action);

        if (animate)
        {
            IconSwapAnimation.Begin();
        }
    }

    // Llamado cuando el tema cambia desde fuera (Configuración, otra app...).
    private void OnSystemThemeChanged() =>
        DispatcherQueue.TryEnqueue(() => ApplyTheme(ThemeService.IsLightTheme(), animate: true));

    private void OnDpiChanged() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            int size = GetBubbleSizePx();
            AppWindow.Resize(new SizeInt32(size, size));
        });

    #region Arrastrar la burbuja

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(ToggleButton).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerDown = true;
        _dragging = false;
        _suppressClick = false;
        NativeMethods.GetCursorPos(out _dragStartCursor);
        _dragStartWindow = AppWindow.Position;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerDown)
        {
            return;
        }

        NativeMethods.GetCursorPos(out NativeMethods.POINT cursor);
        int dx = cursor.X - _dragStartCursor.X;
        int dy = cursor.Y - _dragStartCursor.Y;

        if (!_dragging && (Math.Abs(dx) > DragThresholdPx || Math.Abs(dy) > DragThresholdPx))
        {
            _dragging = true;
            _suppressClick = true; // al soltar no debe cambiar el tema
        }

        if (_dragging)
        {
            AppWindow.Move(new PointInt32(_dragStartWindow.X + dx, _dragStartWindow.Y + dy));
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _pointerDown = false;
        _dragging = false;
    }

    #endregion

    #region Subclass Win32

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint SubclassProc(nint hwnd, uint msg, nuint wParam, nint lParam, nuint id, nuint refData)
    {
        try
        {
            switch (msg)
            {
                case NativeMethods.WM_ERASEBKGND:
                    return 1;

                case NativeMethods.WM_PAINT:
                    // Negro = totalmente transparente con DWM blur-behind
                    nint hdc = NativeMethods.BeginPaint(hwnd, out NativeMethods.PAINTSTRUCT ps);
                    if (hdc != 0)
                    {
                        NativeMethods.FillRect(hdc, ps.rcPaint, NativeMethods.GetStockObject(NativeMethods.BLACK_BRUSH));
                        NativeMethods.EndPaint(hwnd, ps);
                    }
                    return 0;

                case NativeMethods.WM_NCCALCSIZE when wParam != 0:
                    // Sin área no cliente: el rectángulo de la ventana entero es cliente.
                    return 0;

                case NativeMethods.WM_STYLECHANGING when (int)wParam == NativeMethods.GWL_STYLE:
                    // El presenter vuelve a poner WS_DLGFRAME/WS_SYSMENU al mostrarse la ventana.
                    var styles = (NativeMethods.STYLESTRUCT*)lParam;
                    styles->styleNew &= ~FrameStyles;
                    return 0;

                case NativeMethods.WM_NCPAINT:
                    // No hay marco que pintar; DefWindowProc dibujaría la barra de título clásica.
                    return 0;

                case NativeMethods.WM_NCACTIVATE:
                    // lParam = -1: actualiza el estado de activación sin repintar el marco.
                    return NativeMethods.DefSubclassProc(hwnd, msg, wParam, -1);

                case NativeMethods.WM_SETTINGCHANGE:
                    if (lParam != 0 && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
                    {
                        s_instance?.OnSystemThemeChanged();
                    }
                    break;

                case NativeMethods.WM_GETMINMAXINFO:
                    // Windows impone un ancho mínimo (~136 px) a las ventanas; la burbuja es más pequeña.
                    nint minMaxResult = NativeMethods.DefSubclassProc(hwnd, msg, wParam, lParam);
                    var info = (NativeMethods.MINMAXINFO*)lParam;
                    info->ptMinTrackSize = new NativeMethods.POINT { X = 1, Y = 1 };
                    return minMaxResult;

                case NativeMethods.WM_DPICHANGED:
                    nint result = NativeMethods.DefSubclassProc(hwnd, msg, wParam, lParam);
                    s_instance?.OnDpiChanged();
                    return result;

                case NativeMethods.WM_NCDESTROY:
                    NativeMethods.RemoveWindowSubclass(hwnd, &SubclassProc, id);
                    break;
            }
        }
        catch
        {
            // Nunca dejar escapar excepciones a código nativo.
        }

        return NativeMethods.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    #endregion
}
