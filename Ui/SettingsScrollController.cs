using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GDriveTelegramSender.Ui;

internal sealed class SettingsScrollController : IDisposable
{
    private readonly ScrollViewer _scroll;
    private ScrollBar? _bar;
    private Track? _track;
    private readonly DispatcherTimer _hideTimer;
    private bool _dragging;
    private bool _disposed;
    private double _dragOffset;

    internal SettingsScrollController(ScrollViewer scroll)
    {
        _scroll = scroll;
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background, scroll.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(OverlayScrollbarPolicy.HideDelayMilliseconds),
        };
        _hideTimer.Tick += OnHideTimer;

        scroll.ScrollChanged += OnScrollChanged;
        scroll.Loaded += OnScrollLoaded;
        scroll.IsVisibleChanged += OnScrollVisibleChanged;
        scroll.Unloaded += OnUnloaded;
        scroll.PreviewMouseWheel += OnPreviewMouseWheel;
        scroll.MouseMove += OnScrollMouseMove;

        TryAttach();
    }

    private void OnScrollLoaded(object sender, RoutedEventArgs e) => TryAttach();

    private void OnScrollVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_scroll.IsVisible)
        {
            TryAttach();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroll.ScrollableHeight <= 1) return;
        Wake();
    }

    private void OnScrollMouseMove(object sender, MouseEventArgs e)
    {
        if (_scroll.ScrollableHeight <= 1) return;
        var p = e.GetPosition(_scroll);
        if (p.X >= _scroll.ActualWidth - 28)
        {
            Wake();
        }
    }

    private void TryAttach()
    {
        if (_bar != null || _disposed) return;

        _scroll.ApplyTemplate();
        if (_scroll.Template == null) return;

        _bar = _scroll.Template.FindName("PART_VerticalScrollBar", _scroll) as ScrollBar;
        if (_bar == null) return;

        _bar.ApplyTemplate();
        _track = _bar.Template?.FindName("PART_Track", _bar) as Track;

        _bar.PreviewMouseLeftButtonDown += OnPointerDown;
        _bar.PreviewMouseMove += OnPointerMove;
        _bar.PreviewMouseLeftButtonUp += OnPointerUp;
        _bar.LostMouseCapture += OnLostCapture;
        _bar.MouseEnter += OnBarMouseEnter;
        _bar.MouseLeave += OnBarMouseLeave;
    }

    private void Wake()
    {
        if (_bar == null) TryAttach();
        if (_bar == null) return;
        _bar.IsHitTestVisible = true;
        FadeTo(1, OverlayScrollbarPolicy.FadeInMilliseconds);
        ScheduleHide();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.OriginalSource is ScrollViewer childViewer && !ReferenceEquals(childViewer, _scroll)) return;
        if (_bar == null) TryAttach();
        if (_bar == null) return;

        if (_scroll.ScrollableHeight <= 1)
        {
            Reset();
            return;
        }
        if (e.VerticalChange == 0 && e.ExtentHeightChange == 0) return;
        Wake();
    }

    private void FadeTo(double opacity, int milliseconds)
    {
        if (_bar == null) return;
        if (!SystemParameters.ClientAreaAnimation)
        {
            _bar.BeginAnimation(UIElement.OpacityProperty, null);
            _bar.Opacity = opacity;
            return;
        }
        _bar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(opacity,
            TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private void ScheduleHide()
    {
        _hideTimer.Stop();
        if (!_dragging && !_disposed) _hideTimer.Start();
    }

    private void OnHideTimer(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (_dragging || _bar == null) return;
        if (_bar.IsMouseOver) return; // Don't hide while mouse is over scrollbar
        _bar.IsHitTestVisible = false;
        FadeTo(0, OverlayScrollbarPolicy.FadeOutMilliseconds);
    }

    private void OnBarMouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
        if (_bar != null)
        {
            _bar.IsHitTestVisible = true;
            FadeTo(1, OverlayScrollbarPolicy.FadeInMilliseconds);
        }
    }

    private void OnBarMouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleHide();
    }

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_track == null || _bar == null || _scroll.ScrollableHeight <= 1) return;
        var y = e.GetPosition(_track).Y;
        var thumbHeight = _track.Thumb?.ActualHeight ?? 0;
        var top = _scroll.VerticalOffset / _scroll.ScrollableHeight * Math.Max(0, _track.ActualHeight - thumbHeight);
        _dragOffset = y >= top && y <= top + thumbHeight ? y - top : thumbHeight / 2;
        _dragging = _bar.CaptureMouse();
        if (!_dragging) return;
        _hideTimer.Stop();
        ScrollFromPointer(y);
        e.Handled = true;
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _track == null) return;
        ScrollFromPointer(e.GetPosition(_track).Y);
        e.Handled = true;
    }

    private void ScrollFromPointer(double y)
    {
        if (_track == null) return;
        var thumbHeight = _track.Thumb?.ActualHeight ?? 0;
        var range = _track.ActualHeight - thumbHeight;
        if (range <= 0) return;
        _scroll.ScrollToVerticalOffset(Math.Clamp((y - _dragOffset) / range, 0, 1) * _scroll.ScrollableHeight);
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _bar?.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        _dragging = false;
        ScheduleHide();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Reset();

    private void Reset()
    {
        if (_bar != null)
        {
            if (_bar.IsMouseCaptured) _bar.ReleaseMouseCapture();
            _bar.IsHitTestVisible = false;
            _bar.BeginAnimation(UIElement.OpacityProperty, null);
            _bar.Opacity = 0;
        }
        _hideTimer.Stop();
        _dragging = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _hideTimer.Tick -= OnHideTimer;
        _scroll.ScrollChanged -= OnScrollChanged;
        _scroll.Loaded -= OnScrollLoaded;
        _scroll.IsVisibleChanged -= OnScrollVisibleChanged;
        _scroll.Unloaded -= OnUnloaded;
        _scroll.PreviewMouseWheel -= OnPreviewMouseWheel;
        _scroll.MouseMove -= OnScrollMouseMove;
        if (_bar != null)
        {
            _bar.PreviewMouseLeftButtonDown -= OnPointerDown;
            _bar.PreviewMouseMove -= OnPointerMove;
            _bar.PreviewMouseLeftButtonUp -= OnPointerUp;
            _bar.LostMouseCapture -= OnLostCapture;
            _bar.MouseEnter -= OnBarMouseEnter;
            _bar.MouseLeave -= OnBarMouseLeave;
        }
    }
}
