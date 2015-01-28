//http://stackoverflow.com/questions/8631491/how-do-you-pass-the-owner-window-to-show-method-overload

using System;
using System.Windows.Forms;

class ArbitraryWindow : IWin32Window
{
    public ArbitraryWindow(IntPtr handle) { Handle = handle; }
    public IntPtr Handle { get; private set; }
}