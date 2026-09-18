using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GDriveTelegramSender.Ui;

public sealed class ViewTransition : IDisposable
{
    private readonly FrameworkElement _surface;
    private readonly FrameworkElement[] _views;
    private readonly TranslateTransform _translation = new();
    private FrameworkElement _current;
    private FrameworkElement _target;
    private bool _exiting;
    private bool _disposed;
    private int _generation;

    public ViewTransition(FrameworkElement surface, FrameworkElement[] views)
    {
        _surface = surface;
        _views = views;
        _current = _target = Array.Find(views, v => v.Visibility == Visibility.Visible) ?? views[0];
        surface.RenderTransform = _translation;
        surface.IsVisibleChanged += OnVisibilityChanged;
        surface.Unloaded += OnUnloaded;
    }

    public void Show(FrameworkElement targetView)
    {
        if (_disposed || ReferenceEquals(targetView, _target)) return;
        _target = targetView;

        if (!_surface.IsLoaded || !_surface.IsVisible || !UiAnimationPolicy.Enabled)
        {
            Finish();
            return;
        }

        if (_exiting) return;
        _exiting = true;

        Animate(_translation.Y + 12, 0, 85, EasingMode.EaseIn, () =>
        {
            _exiting = false;
            ApplyTarget();
            _translation.Y = 16;
            _surface.Opacity = 0;
            Animate(0, 1, 180, EasingMode.EaseOut, Finish);
        });
    }

    private void ApplyTarget()
    {
        if (ReferenceEquals(_current, _target)) return;
        foreach (var view in _views)
        {
            view.Visibility = ReferenceEquals(view, _target) ? Visibility.Visible : Visibility.Collapsed;
        }
        _current = _target;
        _surface.UpdateLayout();
    }

    private void Animate(double y, double opacity, int milliseconds, EasingMode easing, Action completed)
    {
        var fromY = _translation.Y;
        var fromOpacity = _surface.Opacity;
        StopAnimations();
        var generation = _generation;
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        _translation.Y = fromY;
        _surface.Opacity = fromOpacity;

        _translation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, y, duration)
        {
            EasingFunction = new CubicEase { EasingMode = easing },
        });

        var fade = new DoubleAnimation(fromOpacity, opacity, duration)
        {
            EasingFunction = new CubicEase { EasingMode = easing },
        };
        fade.Completed += (_, _) =>
        {
            if (_disposed || generation != _generation) return;
            StopAnimations();
            _translation.Y = y;
            _surface.Opacity = opacity;
            completed();
        };
        _surface.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void StopAnimations()
    {
        _generation++;
        _translation.BeginAnimation(TranslateTransform.YProperty, null);
        _surface.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private void Finish()
    {
        StopAnimations();
        _exiting = false;
        ApplyTarget();
        _translation.Y = 0;
        _surface.Opacity = 1;
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_surface.IsVisible) Finish();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Finish();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _surface.IsVisibleChanged -= OnVisibilityChanged;
        _surface.Unloaded -= OnUnloaded;
        Finish();
    }
}
