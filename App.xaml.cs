using System.Configuration;
using System.Data;
using System.Windows;

namespace SQLAtlas;

public partial class App : Application
{
    public string ConnectionString { get; set; } = string.Empty;

    public App()
    {
        ConnectionString = string.Empty;
    }
}
