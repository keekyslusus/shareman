using System.Windows;
using System.Windows.Media;

namespace GDriveTelegramSender.Ui;

public static class AppIcons
{
    public static Geometry SendOutlined { get; } = Group(
        Geometry.Parse("m22 2-11 11"),
        Geometry.Parse("M22 2 15 22l-4-9-9-4Z"));

    public static Geometry RocketOutlined { get; } = Group(
        Geometry.Parse("M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"),
        Geometry.Parse("M12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6.05 11a22.35 22.35 0 0 1-3.95 2z"),
        new EllipseGeometry(new Point(15, 9), 1.5, 1.5));

    public static Geometry CloudOutlined { get; } = Group(
        Geometry.Parse("M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z"));

    public static Geometry CloudUploadOutlined { get; } = Group(
        Geometry.Parse("M16 16l-4-4-4 4"),
        Geometry.Parse("M12 12v9"),
        Geometry.Parse("M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3"));

    public static Geometry ChatOutlined { get; } = Group(
        Geometry.Parse("M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"));

    public static Geometry ShareOutlined { get; } = Group(
        new EllipseGeometry(new Point(18, 5), 3, 3),
        new EllipseGeometry(new Point(6, 12), 3, 3),
        new EllipseGeometry(new Point(18, 19), 3, 3),
        Geometry.Parse("M8.59 13.51l6.83 3.98M15.41 6.51l-6.82 3.98"));

    public static Geometry SettingsOutlined { get; } = Group(
        new EllipseGeometry(new Point(12, 12), 3, 3),
        Geometry.Parse("M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"));

    public static Geometry ArrowBackOutlined { get; } = Group(
        Geometry.Parse("M19 12H5"),
        Geometry.Parse("m12 19-7-7 7-7"));

    public static Geometry InfoOutlined { get; } = Group(
        new EllipseGeometry(new Point(12, 12), 9, 9),
        Geometry.Parse("M12 11v6m0-10h.01"));

    public static Geometry FolderOutlined { get; } = Group(
        Geometry.Parse("M3 7V5a2 2 0 0 1 2-2h5l2 3h7a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Z"));

    public static Geometry FolderOpenOutlined { get; } = Group(
        Geometry.Parse("M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"),
        Geometry.Parse("M2 10h20"));

    public static Geometry MovieOutlined { get; } = Group(
        new RectangleGeometry(new Rect(3, 4, 18, 16), 2, 2),
        Geometry.Parse("m10 9 5 3-5 3V9z"));

    public static Geometry SearchOutlined { get; } = Group(
        new EllipseGeometry(new Point(10.5, 10.5), 6.5, 6.5),
        Geometry.Parse("m16 16 5 5"));

    public static Geometry RefreshOutlined { get; } = Group(
        Geometry.Parse("M23 4v6h-6"),
        Geometry.Parse("M1 20v-6h6"),
        Geometry.Parse("M3.51 9a9 9 0 0 1 14.85-3.36L23 10"),
        Geometry.Parse("M1 14l4.64 4.36A9 9 0 0 0 20.49 15"));

    public static Geometry CheckOutlined { get; } = Group(
        Geometry.Parse("m20 6-11 11-5-5"));

    public static Geometry CheckCircleOutlined { get; } = Group(
        new EllipseGeometry(new Point(12, 12), 9, 9),
        Geometry.Parse("m8 12 3 3 5-6"));

    public static Geometry CloseOutlined { get; } = Group(
        Geometry.Parse("m18 6-12 12"),
        Geometry.Parse("m6 6 12 12"));

    public static Geometry KeyOutlined { get; } = Group(
        new EllipseGeometry(new Point(8, 12), 5, 5),
        Geometry.Parse("M13 12h8m-3 0v3m-3-3v2"));

    public static Geometry LockOutlined { get; } = Group(
        new RectangleGeometry(new Rect(4, 10, 16, 11), 2, 2),
        Geometry.Parse("M8 10V7a4 4 0 0 1 8 0v3"));

    public static Geometry PhoneOutlined { get; } = Group(
        Geometry.Parse("M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"));

    public static Geometry DescriptionOutlined { get; } = Group(
        Geometry.Parse("M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"),
        Geometry.Parse("M14 2v6h6M16 13H8m8 4H8m2-8H8"));

    public static Geometry RestoreOutlined { get; } = Group(
        Geometry.Parse("M3 10a9 9 0 1 1 1 8M3 4v6h6"));

    public static Geometry LoginOutlined { get; } = Group(
        Geometry.Parse("M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"),
        Geometry.Parse("m10 17 5-5-5-5"),
        Geometry.Parse("M15 12H3"));

    public static Geometry LogoutOutlined { get; } = Group(
        Geometry.Parse("M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"),
        Geometry.Parse("m16 17 5-5-5-5"),
        Geometry.Parse("M21 12H9"));

    public static Geometry PersonOutlined { get; } = Group(
        Geometry.Parse("M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"),
        new EllipseGeometry(new Point(12, 7), 4, 4));

    public static Geometry UploadOutlined { get; } = Group(
        Geometry.Parse("M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"),
        Geometry.Parse("m17 8-5-5-5 5"),
        Geometry.Parse("M12 3v12"));

    public static Geometry SaveOutlined { get; } = Group(
        Geometry.Parse("M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"),
        Geometry.Parse("M17 21v-8H7v8M7 3v5h8"));

    public static Geometry AddOutlined { get; } = Group(
        Geometry.Parse("M12 5v14"),
        Geometry.Parse("M5 12h14"));

    public static Geometry DeleteOutlined { get; } = Group(
        Geometry.Parse("M3 6h18"),
        Geometry.Parse("M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"),
        Geometry.Parse("M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"));

    public static Geometry TuneOutlined { get; } = Group(
        Geometry.Parse("M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3"),
        Geometry.Parse("M1 14h6M9 8h6M17 16h6"));

    public static Geometry FileOpenOutlined { get; } = Group(
        Geometry.Parse("M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"),
        Geometry.Parse("M14 2v6h6"),
        Geometry.Parse("m12 11 3 3-3 3M9 14h6"));

    public static Geometry DriveOutlined { get; } = Group(
        Geometry.Parse("M9 3h6l6 10.5-3 5.5H6L3 13.5 9 3z"),
        Geometry.Parse("M3 13.5h12"),
        Geometry.Parse("M9 3l6 10.5"));

    public static Geometry TelegramOutlined { get; } = Group(
        Geometry.Parse("M22 2 11 13"),
        Geometry.Parse("M22 2 15 22l-4-9-9-4Z"));

    public static Geometry ShortcutOutlined { get; } = Group(
        Geometry.Parse("M14 3h7v7"),
        Geometry.Parse("m21 3-11 11"),
        Geometry.Parse("M10 5H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-5"));

    public static Geometry ErrorOutlined { get; } = Group(
        new EllipseGeometry(new Point(12, 12), 9, 9),
        Geometry.Parse("M12 8v5"),
        Geometry.Parse("M12 16h.01"));

    public static Geometry LanguageOutlined { get; } = Group(
        new EllipseGeometry(new Point(12, 12), 9, 9),
        Geometry.Parse("M3 12h18M12 3a15.3 15.3 0 0 1 4 9 15.3 15.3 0 0 1-4 9 15.3 15.3 0 0 1-4-9 15.3 15.3 0 0 1 4-9z"));

    public static Geometry EyeOutlined { get; } = Group(
        Geometry.Parse("M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"),
        new EllipseGeometry(new Point(12, 12), 3, 3));

    public static Geometry EyeOffOutlined { get; } = Group(
        Geometry.Parse("M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"),
        Geometry.Parse("M1 1 23 23"));

    private static Geometry Group(params Geometry[] children)
    {
        var geometry = new GeometryGroup();
        foreach (var child in children) geometry.Children.Add(child);
        geometry.Freeze();
        return geometry;
    }
}
