using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NewControls;

namespace Test_ExplodeScript
{
    public class TreeNodeEx : TreeNode
    {
        public TreeNodeEx(uint uHandle, ushort matID, string text, bool isChild)
        {
            this.Text = text;
            this.uHandle = uHandle;
            this.matID = matID;
            this.isChild = isChild;
        }

        public uint uHandle { get; set; }
        public ushort matID { get; set; }
        public bool isChild { get; set; }
        public uint ParentHandle { get; set; }
    }
}




//public class BufferedTreeView : TreeView 
    //{
    //    // Pinvoke:
    //    private const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
    //    private const int TVS_EX_DOUBLEBUFFER = 0x0004;
    //    private const int WM_SETREDRAW = 11;

    //    private const int TVN_FIRST = -400;

    //    private const int TVN_SELCHANGINGW = (TVN_FIRST - 50);

    //    private const int WM_LBUTTONDOWN = 0x201;

    //    private const int WM_REFLECT = 0x2000;
    //    private const int WM_NOFITY = 0x004e;

    //    private const int WM_PAINT = 0x000F;
    //    private const int WM_PRINTCLIENT = 0x0318;
    //    private const int WM_ERASEBKGND = 0x0014;


    //    [StructLayout(LayoutKind.Sequential)]
    //    private struct NMHDR
    //    {
    //        public IntPtr hwndFrom;
    //        public IntPtr idFrom;
    //        public int code;
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    private struct NMTREEVIEW
    //    {
    //        public NMHDR hdr;
    //        public int action;
    //        public TVITEM itemOld;
    //        public TVITEM itemNew;
    //        public POINT ptDrag;
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    public struct POINT
    //    {
    //        public int X;
    //        public int Y;
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    private struct TVITEM
    //    {
    //        public uint mask;
    //        public IntPtr hItem;
    //        public uint state;
    //        public uint stateMask;
    //        public IntPtr pszText;
    //        public int cchTextMax;
    //        public int iImage;
    //        public int iSelectedImage;
    //        public int cChildren;
    //        public IntPtr lParam;
    //    }

    //    [DllImport("user32.dll")]
    //    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    //    [DllImport("user32.dll")]
    //    public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);

    //    Bitmap   internalBitmap = null;
    //    Graphics internalGraphics = null;

    //    private void DisposeInternal()
    //    {
    //        if (internalGraphics != null)
    //            internalGraphics.Dispose();
    //        if (internalBitmap != null)
    //            internalBitmap.Dispose();
    //    }

    //    /// <summary>Releases resources.</summary>
    //    /// <param name="disposing">true = Both managed and unmanaged, false = Unmanaged only.</param>
    //    protected override void Dispose(bool disposing)
    //    {
    //        if (disposing)
    //            DisposeInternal();
    //        base.Dispose(disposing);
    //    }

    //    /// <summary>Occurs when window is resized.</summary>
    //    /// <param name="e">A System.EventArgs.Empty.</param>
    //    /// <remarks>Recreates internal Graphics object. </remarks>
    //    protected override void OnResize(System.EventArgs e)
    //    {
    //        if (internalBitmap == null ||
    //            internalBitmap.Width != Width || internalBitmap.Height != Height)
    //        {

    //            if (Width != 0 && Height != 0)
    //            {
    //                DisposeInternal();
    //                internalBitmap = new Bitmap(Width, Height);
    //                internalGraphics = Graphics.FromImage(internalBitmap);
    //            }
    //        }
    //    }


    //    protected override void OnHandleCreated(EventArgs e) 
    //    {
    //        SendMessage(Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
    //        base.OnHandleCreated(e);
    //    }

    //    protected override void OnPaint(PaintEventArgs e)
    //    {
    //        //using (var pen = new Pen(Color.FromArgb(30, 30, 30)))
    //        //{
    //        //    e.Graphics.DrawLine(pen, new Point(ClientRectangle.Left, ClientRectangle.Top), new Point(ClientRectangle.Left + ClientRectangle.Width, ClientRectangle.Top));
    //        //    e.Graphics.DrawLine(pen, new Point(ClientRectangle.Left, ClientRectangle.Bottom -1), new Point(ClientRectangle.Left + ClientRectangle.Width, ClientRectangle.Bottom -1));
    //        //}

    //        base.OnPaint(e);
    //    }

    //    public void SuspendDrawing()
    //    {
    //        SendMessage(Handle, WM_SETREDRAW, false, 0);
    //    }
        
    //    public void ResumeDrawing()
    //    {
    //        SendMessage(Handle, WM_SETREDRAW, true, 0);
    //        Refresh();
    //    }

    //    protected override void WndProc(ref Message message)
    //    {
    //        switch (message.Msg)
    //        {
    //            case WM_LBUTTONDOWN:
    //            {
    //                SuspendDrawing();
    //                break;
    //            }
    //            case WM_REFLECT + WM_NOFITY:
    //            {
    //                var nmhdr = (NMHDR)Marshal.PtrToStructure(message.LParam, typeof(NMHDR));

    //                if (nmhdr.code == TVN_SELCHANGINGW)
    //                    ResumeDrawing();

    //                break;
    //            }

    //            case WM_ERASEBKGND:
    //            //removes flicker
    //            return;

    //            case WM_PAINT:
    //            // The designer host does not call OnResize()                    
    //            if (internalGraphics == null)
    //                OnResize(EventArgs.Empty);

    //            //Set up 
    //            Win32.RECT updateRect = new Win32.RECT();
    //            if (Win32.GetUpdateRect(message.HWnd, ref updateRect, false) == 0)
    //                break;

    //            Win32.PAINTSTRUCT paintStruct = new Win32.PAINTSTRUCT();
    //            IntPtr screenHdc = Win32.BeginPaint(message.HWnd, ref paintStruct);
    //            using (Graphics screenGraphics = Graphics.FromHdc(screenHdc))
    //            {

    //                //Draw Internal Graphics
    //                IntPtr hdc = internalGraphics.GetHdc();
    //                Message printClientMessage = Message.Create(Handle, WM_PRINTCLIENT, hdc, IntPtr.Zero);
    //                DefWndProc(ref printClientMessage);
    //                internalGraphics.ReleaseHdc(hdc);

    //                //Add the missing OnPaint() call
    //                OnPaint(new PaintEventArgs(internalGraphics, Rectangle.FromLTRB(
    //                    updateRect.left,
    //                    updateRect.top,
    //                    updateRect.right,
    //                    updateRect.bottom)));


    //                //Draw Screen Graphics
    //                screenGraphics.DrawImage(internalBitmap, 0, 0);
    //            }

    //            //Tear down
    //            Win32.EndPaint(message.HWnd, ref paintStruct);
    //            return;
    //        }
    //        base.WndProc(ref message);
    //    }
    //}



