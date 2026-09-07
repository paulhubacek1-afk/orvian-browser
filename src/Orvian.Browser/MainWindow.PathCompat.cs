namespace Orvian.Browser;

public partial class MainWindow
{
    // WPF has System.Windows.Shapes.Path while the browser code also needs System.IO.Path.
    // A nested compatibility type lets both existing call sites compile without changing
    // the browser's generated UI code or its file-path behavior.
    private class Path : System.Windows.Shapes.Path
    {
        public static string Combine(params string[] paths) => System.IO.Path.Combine(paths);
        public static string? GetDirectoryName(string? path) => System.IO.Path.GetDirectoryName(path);
        public static string? GetFileName(string? path) => System.IO.Path.GetFileName(path);
        public static string GetTempPath() => System.IO.Path.GetTempPath();
    }
}
