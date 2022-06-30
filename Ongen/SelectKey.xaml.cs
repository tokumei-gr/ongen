using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ongen
{
    public sealed partial class SelectKey : ContentDialog
    {
        ObservableCollection<string> keys = new(Properties.Resources.keys.Split("\n", StringSplitOptions.TrimEntries));
        string selectedKey;

        public SelectKey(string selectedKey)
        {
            this.selectedKey = selectedKey;
            this.InitializeComponent();
        }
    }
}
