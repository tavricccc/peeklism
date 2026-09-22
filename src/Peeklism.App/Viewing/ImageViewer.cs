using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace Peeklism.App.Viewing;

internal sealed class ImageViewer : Grid, IDisposable
{
    // Resize the layout rather than ScrollViewer.ZoomFactor: its minimum 10% cannot fit panoramas.
    private readonly ScrollViewer _scroll = new()
    {
        ZoomMode = ZoomMode.Disabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Enabled,
    };
    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private bool _fit = true;
    private bool _disposed;
    private int _rotation;
    private double _zoom = 1;
    private Point? _drag;
    private double _dragX, _dragY;
    private uint _width, _height;
    public event Action<string>? StatusChanged;

    public ImageViewer()
    {
        _canvas.Children.Add(_image);
        _scroll.Content = _canvas;
        Children.Add(_scroll);
        SizeChanged += (_, _) => { if (_fit) Fit(); };
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, e) => { _drag = null; _canvas.ReleasePointerCapture(e.Pointer); };
        _canvas.PointerCaptureLost += (_, _) => _drag = null;
        _canvas.DoubleTapped += (_, _) => { if (_fit) Actual(); else Fit(); };
        AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnWheel), true);
    }

    public async Task LoadAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            (_width, _height) = await Task.Run(() => ReadSvgDimensions(path));
            if (_disposed) return;
            var svg = new SvgImageSource();
            var result = await svg.SetSourceAsync(stream);
            if (_disposed) return;
            if (result != SvgImageSourceLoadStatus.Success) throw new IOException("SVG 格式無法解碼。");
            _image.Source = svg;
        }
        else
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 160_000_000)
                throw new IOException("圖片超過 1.6 億像素，為避免耗盡記憶體，請使用支援分塊讀取的影像工具。");
            stream.Seek(0);
            // Explicit physical decode size defeats WinUI's display-size decode optimisation.
            var bitmap = new BitmapImage
            {
                DecodePixelType = DecodePixelType.Physical,
                DecodePixelWidth = (int)decoder.OrientedPixelWidth,
                CreateOptions = BitmapCreateOptions.IgnoreImageCache,
            };
            await bitmap.SetSourceAsync(stream);
            if (_disposed) return;
            _width = (uint)bitmap.PixelWidth;
            _height = (uint)bitmap.PixelHeight;
            _image.Source = bitmap;
        }
        LayoutImage();
        Fit();
    }

    private static (uint Width, uint Height) ReadSvgDimensions(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024,
        });
        var root = XDocument.Load(reader).Root ?? throw new IOException("SVG 沒有根元素。");
        var viewBox = ((string?)root.Attribute("viewBox"))?.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        static double Parse(string? value) => double.TryParse(value?.Trim().Replace("px", "", StringComparison.Ordinal),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) && parsed > 0 ? parsed : 0;
        var width = Parse((string?)root.Attribute("width"));
        var height = Parse((string?)root.Attribute("height"));
        if ((width <= 0 || height <= 0) && viewBox?.Length == 4) { width = Parse(viewBox[2]); height = Parse(viewBox[3]); }
        return ((uint)Math.Clamp(width > 0 ? width : 300, 1, 1_000_000),
            (uint)Math.Clamp(height > 0 ? height : 150, 1, 1_000_000));
    }

    private void LayoutImage()
    {
        _image.Width = _width * _zoom;
        _image.Height = _height * _zoom;
        // Canvas does not clip an unrotated wide image into the rotated portrait layout slot.
        _image.RenderTransform = new CompositeTransform
        {
            Rotation = _rotation,
            TranslateX = _rotation switch { 90 => _height * _zoom, 180 => _width * _zoom, _ => 0 },
            TranslateY = _rotation switch { 180 => _height * _zoom, 270 => _width * _zoom, _ => 0 },
        };
        _canvas.Width = (_rotation % 180 == 0 ? _width : _height) * _zoom;
        _canvas.Height = (_rotation % 180 == 0 ? _height : _width) * _zoom;
        if (_image.Source is SvgImageSource svg)
        {
            var dpi = XamlRoot?.RasterizationScale ?? 1;
            var scale = Math.Min(dpi * _zoom, Math.Min(8192d / Math.Max(_width, _height),
                Math.Sqrt(32_000_000d / ((double)_width * _height))));
            svg.RasterizePixelWidth = Math.Max(1, _width * scale);
            svg.RasterizePixelHeight = Math.Max(1, _height * scale);
        }
    }

    public void Fit()
    {
        _fit = true;
        if (_width == 0 || ActualWidth < 1 || ActualHeight < 1) return;
        var width = _rotation % 180 == 0 ? _width : _height;
        var height = _rotation % 180 == 0 ? _height : _width;
        var factor = Math.Min((ActualWidth - 24) / width, (ActualHeight - 24) / height);
        SetZoom(Math.Min(factor, 1 / (XamlRoot?.RasterizationScale ?? 1)));
    }

    public void Actual()
    {
        _fit = false;
        SetZoom(1 / (XamlRoot?.RasterizationScale ?? 1));
    }

    public void Zoom(double multiplier)
    {
        _fit = false;
        SetZoom(_zoom * multiplier);
    }

    public void Rotate()
    {
        _rotation = (_rotation + 90) % 360;
        LayoutImage();
        if (_fit) Fit();
        else
        {
            _scroll.UpdateLayout();
            _scroll.ChangeView(Math.Max(0, (_canvas.Width - ActualWidth) / 2),
                Math.Max(0, (_canvas.Height - ActualHeight) / 2), null, true);
        }
    }

    private void SetZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, .00001, 32);
        var ratio = zoom / _zoom;
        var marginX = Math.Max(0, (ActualWidth - _canvas.Width) / 2);
        var marginY = Math.Max(0, (ActualHeight - _canvas.Height) / 2);
        var x = (_scroll.HorizontalOffset + ActualWidth / 2 - marginX) * ratio - ActualWidth / 2;
        var y = (_scroll.VerticalOffset + ActualHeight / 2 - marginY) * ratio - ActualHeight / 2;
        _zoom = zoom;
        LayoutImage();
        _scroll.UpdateLayout();
        _scroll.ChangeView(Math.Max(0, x), Math.Max(0, y), null, true);
        StatusChanged?.Invoke($"{_width:N0} × {_height:N0}  ·  {_zoom * (XamlRoot?.RasterizationScale ?? 1):P0}");
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == 0) return;
        Zoom(Math.Pow(1.2, e.GetCurrentPoint(this).Properties.MouseWheelDelta / 120d));
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_scroll);
        if (!point.Properties.IsLeftButtonPressed || point.PointerDeviceType != PointerDeviceType.Mouse) return;
        _drag = point.Position;
        _dragX = _scroll.HorizontalOffset;
        _dragY = _scroll.VerticalOffset;
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is not { } start) return;
        var point = e.GetCurrentPoint(_scroll).Position;
        _scroll.ChangeView(_dragX + start.X - point.X, _dragY + start.Y - point.Y, null, true);
        e.Handled = true;
    }

    public void Dispose()
    {
        _disposed = true;
        _image.Source = null;
        _scroll.Content = null;
    }
}
