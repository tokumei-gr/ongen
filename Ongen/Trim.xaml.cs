using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ongen
{
    public sealed partial class Trim : ContentDialog
    {
        int startPos = 0;
        int endPos = 0;
        public Trim(int startPos, int endPos)
        {
            this.startPos = startPos;
            this.endPos = endPos;

            this.InitializeComponent();

            numericLeft.Maximum = (double)Decimal.MaxValue;
            numericRight.Maximum = (double)Decimal.MaxValue;
        }
    }
}
