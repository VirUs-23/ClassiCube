#region --- License ---
/* Licensed under the MIT/X11 license.
 * Copyright (c) 2006-2008 the OpenTK Team.
 * This notice may not be removed from any source distribution.
 * See license.txt for licensing detailed licensing details.
 */
#endregion

using System;
using System.Runtime.InteropServices;
using Window = System.IntPtr;
using KeySym = System.IntPtr;
using Display = System.IntPtr;
using Bool = System.Boolean;
using Status = System.Int32;
    
namespace OpenTK.Platform.X11 {
	
    internal static partial class Functions {
        static readonly object Lock = API.Lock;

        [DllImport("libX11")]
        public extern static IntPtr XOpenDisplay(IntPtr display);
        public static IntPtr XOpenDisplay_Safe(IntPtr display) {
            lock (Lock) {
                return XOpenDisplay(display);
            }
        }

        [DllImport("libX11")]
        public extern static int XCloseDisplay(IntPtr display);

        [DllImport("libX11")]
        public extern static IntPtr XCreateWindow(IntPtr display, IntPtr parent, int x, int y, int width, int height, int border_width, int depth, int xclass, IntPtr visual, IntPtr valuemask, ref XSetWindowAttributes attributes);

        [DllImport("libX11")]
        public extern static int XMapWindow(IntPtr display, IntPtr window);
        [DllImport("libX11")]
        public extern static int XUnmapWindow(IntPtr display, IntPtr window);
        [DllImport("libX11")]
        public extern static IntPtr XRootWindow(IntPtr display, int screen_number);

        [DllImport("libX11")]
        public extern static Bool XCheckWindowEvent(Display display, Window w, EventMask event_mask, ref XEvent event_return);
        [DllImport("libX11")]
        public extern static Bool XCheckTypedWindowEvent(Display display, Window w, XEventName event_type, ref XEvent event_return);

        [DllImport("libX11")]
        public extern static int XDestroyWindow(IntPtr display, IntPtr window);

        [DllImport("libX11")]
        public extern static int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, int width, int height);

        [DllImport("libX11")]
        public extern static int XMoveWindow(IntPtr display, IntPtr w, int x, int y);
        
        [DllImport("libX11")]
        public extern static int XResizeWindow(IntPtr display, IntPtr window, int width, int height);

        [DllImport("libX11")]
        public extern static int XFlush(IntPtr display);

        [DllImport("libX11")]
        public extern static int XStoreName(IntPtr display, IntPtr window, string window_name);

        [DllImport("libX11")]
        public extern static int XFetchName(IntPtr display, IntPtr window, ref IntPtr window_name);

        [DllImport("libX11")]
        public extern static int XSendEvent(IntPtr display, IntPtr window, bool propagate, IntPtr event_mask, ref XEvent send_event);

        public static int XSendEvent(IntPtr display, IntPtr window, bool propagate, EventMask event_mask, ref XEvent send_event) {
            return XSendEvent(display, window, propagate, new IntPtr((int)event_mask), ref send_event);
        }

        [DllImport("libX11")]
        public extern static int XFree(IntPtr data);

        [DllImport("libX11")]
        public extern static int XRaiseWindow(IntPtr display, IntPtr window);

        [DllImport("libX11")]
        public extern static IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport("libX11")]
        public extern static int XSetWMProtocols(IntPtr display, IntPtr window, IntPtr[] protocols, int count);

        [DllImport("libX11")]
        public extern static bool XTranslateCoordinates(IntPtr display, IntPtr src_w, IntPtr dest_w, int src_x, int src_y, out int intdest_x_return, out int dest_y_return, out IntPtr child_return);

        // Colormaps
        [DllImport("libX11")]//, CLSCompliant(false)]
        public extern static uint XDefaultDepth(IntPtr display, int screen_number);

        [DllImport("libX11")]
        public extern static int XDefaultScreen(IntPtr display);

        [DllImport("libX11")]
        public extern static int XSetTransientForHint(IntPtr display, IntPtr window, IntPtr prop_window);

        [DllImport("libX11")]
        public extern static int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, PropertyMode mode, ref MotifWmHints data, int nelements);

        [DllImport("libX11")]
        public extern static int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, PropertyMode mode, IntPtr[] data, int nelements);

        [DllImport("libX11")]
        public extern static int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

        // Drawing
        [DllImport("libX11")]
        public extern static IntPtr XCreateGC(IntPtr display, IntPtr window, IntPtr valuemask, XGCValues[] values);

        [DllImport("libX11")]
        public extern static int XFreeGC(IntPtr display, IntPtr gc);

        [DllImport("libX11")]
        public extern static int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr atom, IntPtr long_offset, IntPtr long_length, bool delete, IntPtr req_type, out IntPtr actual_type, out int actual_format, out IntPtr nitems, out IntPtr bytes_after, ref IntPtr prop);

        [DllImport("libX11")]
        public extern static int XIconifyWindow(IntPtr display, IntPtr window, int screen_number);

        [DllImport("libX11")]
        public extern static IntPtr XCreateFontCursor(IntPtr display, CursorFontShape shape);

        [DllImport("libX11")]//, CLSCompliant(false)]
        public extern static IntPtr XCreatePixmapCursor(IntPtr display, IntPtr source, IntPtr mask, ref XColor foreground_color, ref XColor background_color, int x_hot, int y_hot);

        [DllImport("libX11")]
        public extern static IntPtr XCreatePixmapFromBitmapData(IntPtr display, IntPtr drawable, byte[] data, int width, int height, IntPtr fg, IntPtr bg, int depth);

        [DllImport("libX11")]
        public extern static IntPtr XCreatePixmap(IntPtr display, IntPtr d, int width, int height, int depth);

        [DllImport("libX11")]
        public extern static IntPtr XFreePixmap(IntPtr display, IntPtr pixmap);

        [DllImport("libX11")]
        public extern static int XGetWMNormalHints(IntPtr display, IntPtr window, ref XSizeHints hints, out IntPtr supplied_return);

        [DllImport("libX11")]
        public extern static void XSetWMNormalHints(IntPtr display, IntPtr window, ref XSizeHints hints);

        [DllImport("libX11")]
        public static extern IntPtr XGetWMHints(Display display, Window w); // returns XWMHints*

        [DllImport("libX11")]
        public static extern void XSetWMHints(Display display, Window w, ref XWMHints wmhints);

        [DllImport("libX11")]
        public static extern IntPtr XAllocWMHints();

        [DllImport("libX11")]
        public extern static bool XkbSetDetectableAutoRepeat(IntPtr display, bool detectable, out bool supported);

        [DllImport("libX11")]
        static extern IntPtr XGetVisualInfo(IntPtr display, IntPtr vinfo_mask, ref XVisualInfo template, out int nitems);
                                                    
        public static IntPtr XGetVisualInfo(IntPtr display, XVisualInfoMask vinfo_mask, ref XVisualInfo template, out int nitems) {
            return XGetVisualInfo(display, (IntPtr)(int)vinfo_mask, ref template, out nitems);
        }
        
        [DllImport("libX11")]
        public static extern IntPtr XCreateColormap(Display display, Window window, IntPtr visual, int alloc);

        [DllImport("libX11")]
        public static extern Status XGetTransientForHint(Display display, Window w, out Window prop_window_return);

        [DllImport("libX11")]
        public static extern void XSync(Display display, bool discard);

        [DllImport("libX11")]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11")]
        public static extern int XBitmapBitOrder(Display display);

        [DllImport("libX11")]
        public static extern IntPtr XCreateImage(Display display, IntPtr visual,
            uint depth, ImageFormat format, int offset, byte[] data, uint width, uint height,
            int bitmap_pad, int bytes_per_line);

        [DllImport("libX11")]
        public static extern IntPtr XCreateImage(Display display, IntPtr visual,
            uint depth, ImageFormat format, int offset, IntPtr data, uint width, uint height,
            int bitmap_pad, int bytes_per_line);

        [DllImport("libX11")]
        public static extern void XPutImage(Display display, IntPtr drawable,
            IntPtr gc, IntPtr image, int src_x, int src_y, int dest_x, int dest_y, uint width, uint height);

        [DllImport("libX11")]
        public static extern int XLookupString(ref XKeyEvent event_struct, [Out] byte[] buffer_return,
            int bytes_buffer, [Out] KeySym[] keysym_return, IntPtr status_in_out);

        [DllImport("libX11")]
        public static extern int XRefreshKeyboardMapping(ref XMappingEvent event_map);

        static readonly IntPtr CopyFromParent = IntPtr.Zero;

        public static void SendNetWMMessage(X11WindowInfo window, IntPtr message_type, IntPtr l0, IntPtr l1, IntPtr l2)
        {
            XEvent xev;

            xev = new XEvent();
            xev.ClientMessageEvent.type = XEventName.ClientMessage;
            xev.ClientMessageEvent.send_event = true;
            xev.ClientMessageEvent.window = window.WindowHandle;
            xev.ClientMessageEvent.message_type = message_type;
            xev.ClientMessageEvent.format = 32;
            xev.ClientMessageEvent.ptr1 = l0;
            xev.ClientMessageEvent.ptr2 = l1;
            xev.ClientMessageEvent.ptr3 = l2;

            XSendEvent(window.Display, window.RootWindow, false,
                       new IntPtr((int)(EventMask.SubstructureRedirectMask | EventMask.SubstructureNotifyMask)),
                       ref xev);
        }

        public static IntPtr CreatePixmapFromImage(Display display, System.Drawing.Bitmap image) 
        { 
            int width = image.Width;
            int height = image.Height;
            int size = width * height; 

            System.Drawing.Imaging.BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            IntPtr ximage = XCreateImage(display, CopyFromParent, 24, ImageFormat.ZPixmap, 
                0, data.Scan0, (uint)width, (uint)height, 32, 0); 
            IntPtr pixmap = XCreatePixmap(display, XDefaultRootWindow(display), 
                width, height, 24); 
            IntPtr gc = XCreateGC(display, pixmap, IntPtr.Zero, null);
            
            XPutImage(display, pixmap, gc, ximage, 0, 0, 0, 0, (uint)width, (uint)height);
            
            XFreeGC(display, gc);
            image.UnlockBits(data);
                                         
            return pixmap; 
        } 
        
        public static IntPtr CreateMaskFromImage(Display display, System.Drawing.Bitmap image) 
        { 
            int width = image.Width; 
            int height = image.Height; 
            int stride = (width + 7) >> 3; 
            byte[] mask = new byte[stride * height];
            bool msbfirst = (XBitmapBitOrder(display) == 1); // 1 = MSBFirst
        
            for (int y = 0; y < height; ++y) 
            { 
                for (int x = 0; x < width; ++x) 
                { 
                    byte bit = (byte) (1 << (msbfirst ? (7 - (x & 7)) : (x & 7))); 
                    int offset = y * stride + (x >> 3); 
        
                    if (image.GetPixel(x, y).A >= 128) 
                        mask[offset] |= bit; 
                } 
            } 
        
            return XCreatePixmapFromBitmapData(display, XDefaultRootWindow(display), 
                mask, width, height, new IntPtr(1), IntPtr.Zero, 1); 
        }
    }
}
