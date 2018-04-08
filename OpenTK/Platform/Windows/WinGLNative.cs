#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenTK.Input;

namespace OpenTK.Platform.Windows
{
	/// <summary>
	/// Drives GameWindow on Windows.
	/// This class supports OpenTK, and is not intended for use by OpenTK programs.
	/// </summary>
	internal sealed class WinGLNative : INativeWindow
	{
		const ExtendedWindowStyle ParentStyleEx = ExtendedWindowStyle.WindowEdge | ExtendedWindowStyle.ApplicationWindow;
		readonly IntPtr Instance = Marshal.GetHINSTANCE(typeof(WinGLNative).Module);
		readonly IntPtr ClassName = Marshal.StringToHGlobalAuto("CS_WindowClass");
		readonly WindowProcedure WindowProcedureDelegate;

		bool class_registered, disposed, exists;
		WinWindowInfo window;
		WindowState windowState = WindowState.Normal;
		bool focused;
		bool mouse_outside_window = true;
		bool invisible_since_creation; // Set by WindowsMessage.CREATE and consumed by Visible = true (calls BringWindowToFront).
		int suppress_resize; // Used in WindowBorder and WindowState in order to avoid rapid, consecutive resize events.

		Rectangle bounds = new Rectangle(), client_rectangle = new Rectangle(),
		previous_bounds = new Rectangle(); // Used to restore previous size when leaving fullscreen mode.
		Icon icon;

		KeyboardDevice keyboard = new KeyboardDevice();
		MouseDevice mouse = new MouseDevice();
		static readonly WinKeyMap KeyMap = new WinKeyMap();
		const long ExtendedBit = 1 << 24;           // Used to distinguish left and right control, alt and enter keys.
		KeyPressEventArgs key_press = new KeyPressEventArgs();

		public WinGLNative(int x, int y, int width, int height, string title, DisplayDevice device) {
			WindowProcedureDelegate = WindowProcedure;
			UngroupFromTaskbar();
			window = new WinWindowInfo(CreateWindow(x, y, width, height, title, device));
			exists = true;
		}
		
		void UngroupFromTaskbar() {
			Version version = Environment.OSVersion.Version;
			if ((version.Major > 6) || (version.Major == 6 && version.Minor >= 1)) {
				API.SetCurrentProcessExplicitAppUserModelID("ClassicalSharp_" + new Random().Next());
			}
		}

		unsafe IntPtr WindowProcedure(IntPtr handle, WindowMessage message, IntPtr wParam, IntPtr lParam) {
			switch (message) {
					
					#region Size / Move / Style events

				case WindowMessage.ACTIVATE:
					// See http://msdn.microsoft.com/en-us/library/ms646274(VS.85).aspx (WM_ACTIVATE notification):
					// wParam: The low-order word specifies whether the window is being activated or deactivated.
					bool new_focused_state = Focused;
					focused = (wParam.ToInt64() & 0xFFFF) != 0;

					if (new_focused_state != Focused && FocusedChanged != null)
						FocusedChanged(this, EventArgs.Empty);
					break;

				case WindowMessage.ENTERMENULOOP:
				case WindowMessage.ENTERSIZEMOVE:
				case WindowMessage.EXITMENULOOP:
				case WindowMessage.EXITSIZEMOVE:
					break;

				case WindowMessage.ERASEBKGND:
					return new IntPtr(1);

				case WindowMessage.WINDOWPOSCHANGED:
					WindowPosition* pos = (WindowPosition*)lParam;
					if (window != null && pos->hwnd == window.handle) {
						Point new_location = new Point(pos->x, pos->y);
						if (Location != new_location) {
							bounds.Location = new_location;
							if (Move != null)
								Move(this, EventArgs.Empty);
						}

						Size new_size = new Size(pos->cx, pos->cy);
						if (Size != new_size) {
							bounds.Width = pos->cx;
							bounds.Height = pos->cy;

							Win32Rectangle rect;
							API.GetClientRect(handle, out rect);
							client_rectangle = rect.ToRectangle();

							API.SetWindowPos(window.handle, IntPtr.Zero,
							                 bounds.X, bounds.Y, bounds.Width, bounds.Height,
							                 SetWindowPosFlags.NOZORDER | SetWindowPosFlags.NOOWNERZORDER |
							                 SetWindowPosFlags.NOACTIVATE | SetWindowPosFlags.NOSENDCHANGING);
							if (suppress_resize <= 0 && Resize != null)
								Resize(this, EventArgs.Empty);
						}
					}
					break;

				case WindowMessage.STYLECHANGED:
					if (wParam.ToInt64() == (long)GWL.STYLE) {
						WindowStyle style = ((StyleStruct*)lParam)->New;
						if ((style & WindowStyle.Popup) != 0)
							hiddenBorder = true;
						else if ((style & WindowStyle.ThickFrame) != 0)
							hiddenBorder = false;
					}
					break;

				case WindowMessage.SIZE:
					SizeMessage state = (SizeMessage)wParam.ToInt64();
					WindowState new_state = windowState;
					switch (state) {
							case SizeMessage.RESTORED: new_state = WindowState.Normal; break;
							case SizeMessage.MINIMIZED: new_state = WindowState.Minimized; break;
							case SizeMessage.MAXIMIZED: new_state = hiddenBorder ?
								WindowState.Fullscreen : WindowState.Maximized;
							break;
					}

					if (new_state != windowState) {
						windowState = new_state;
						if (WindowStateChanged != null)
							WindowStateChanged(this, EventArgs.Empty);
					}
					break;

					#endregion

					#region Input events

				case WindowMessage.CHAR:
					key_press.KeyChar = (char)wParam.ToInt64();

					if (KeyPress != null)
						KeyPress(this, key_press);
					break;

				case WindowMessage.MOUSEMOVE:
					Point point = new Point(
						(short)((uint)lParam.ToInt32() & 0x0000FFFF),
						(short)(((uint)lParam.ToInt32() & 0xFFFF0000) >> 16));
					mouse.Position = point;

					if (mouse_outside_window) {
						// Once we receive a mouse move event, it means that the mouse has
						// re-entered the window.
						mouse_outside_window = false;
						EnableMouseTracking();

						if (MouseEnter != null)
							MouseEnter(this, EventArgs.Empty);
					}
					break;

				case WindowMessage.MOUSELEAVE:
					mouse_outside_window = true;
					// Mouse tracking is disabled automatically by the OS

					if (MouseLeave != null)
						MouseLeave(this, EventArgs.Empty);
					// Set all mouse buttons to off when user leaves window, prevents them being stuck down.
					for( MouseButton btn = 0; btn < MouseButton.LastButton; btn++ )
						mouse[btn] = false;
					break;

				case WindowMessage.MOUSEWHEEL:
					// This is due to inconsistent behavior of the WParam value on 64bit arch, whese
					// wparam = 0xffffffffff880000 or wparam = 0x00000000ff100000
					mouse.Wheel += ((long)wParam << 32 >> 48) / 120.0f;
					return IntPtr.Zero;

				case WindowMessage.LBUTTONDOWN:
					mouse[MouseButton.Left] = true;
					break;

				case WindowMessage.MBUTTONDOWN:
					mouse[MouseButton.Middle] = true;
					break;

				case WindowMessage.RBUTTONDOWN:
					mouse[MouseButton.Right] = true;
					break;

				case WindowMessage.XBUTTONDOWN:
					keyboard[(((ulong)wParam.ToInt64() >> 16) & 0xFFFF) == 1 ? Key.XButton1 : Key.XButton2] = true;
					break;

				case WindowMessage.LBUTTONUP:
					mouse[MouseButton.Left] = false;
					break;

				case WindowMessage.MBUTTONUP:
					mouse[MouseButton.Middle] = false;
					break;

				case WindowMessage.RBUTTONUP:
					mouse[MouseButton.Right] = false;
					break;

				case WindowMessage.XBUTTONUP:
					keyboard[(((ulong)wParam.ToInt64() >> 16) & 0xFFFF) == 1 ? Key.XButton1 : Key.XButton2] = false;
					break;

					// Keyboard events:
				case WindowMessage.KEYDOWN:
				case WindowMessage.KEYUP:
				case WindowMessage.SYSKEYDOWN:
				case WindowMessage.SYSKEYUP:
					bool pressed = message == WindowMessage.KEYDOWN ||
						message == WindowMessage.SYSKEYDOWN;

					// Shift/Control/Alt behave strangely when e.g. ShiftRight is held down and ShiftLeft is pressed
					// and released. It looks like neither key is released in this case, or that the wrong key is
					// released in the case of Control and Alt.
					// To combat this, we are going to release both keys when either is released. Hacky, but should work.
					// Win95 does not distinguish left/right key constants (GetAsyncKeyState returns 0).
					// In this case, both keys will be reported as pressed.

					bool extended = (lParam.ToInt64() & ExtendedBit) != 0;
					switch ((VirtualKeys)wParam)
					{
						case VirtualKeys.SHIFT:
							// The behavior of this key is very strange. Unlike Control and Alt, there is no extended bit
							// to distinguish between left and right keys. Moreover, pressing both keys and releasing one
							// may result in both keys being held down (but not always).
							bool lShiftDown = (API.GetKeyState( (int)VirtualKeys.LSHIFT ) >> 15) == 1;
							bool rShiftDown = (API.GetKeyState( (int)VirtualKeys.RSHIFT ) >> 15) == 1;
							
							if( !pressed || lShiftDown != rShiftDown ) {
								Keyboard[Input.Key.ShiftLeft] = lShiftDown;
								Keyboard[Input.Key.ShiftRight] = rShiftDown;
							}
							return IntPtr.Zero;

						case VirtualKeys.CONTROL:
							if (extended)
								keyboard[Input.Key.ControlRight] = pressed;
							else
								keyboard[Input.Key.ControlLeft] = pressed;
							return IntPtr.Zero;

						case VirtualKeys.MENU:
							if (extended)
								keyboard[Input.Key.AltRight] = pressed;
							else
								keyboard[Input.Key.AltLeft] = pressed;
							return IntPtr.Zero;

						case VirtualKeys.RETURN:
							if (extended)
								keyboard[Key.KeypadEnter] = pressed;
							else
								keyboard[Key.Enter] = pressed;
							return IntPtr.Zero;

						default:
							Key tkKey;
							if (!KeyMap.TryGetMappedKey((VirtualKeys)wParam, out tkKey)) {
								Debug.Print("Virtual key {0} ({1}) not mapped.", (VirtualKeys)wParam, lParam.ToInt64());
								break;
							} else{
								keyboard[tkKey] = pressed;
							}
							return IntPtr.Zero;
					}
					break;

				case WindowMessage.SYSCHAR:
					return IntPtr.Zero;

				case WindowMessage.KILLFOCUS:
					keyboard.ClearKeys();
					break;

					#endregion

					#region Creation / Destruction events

				case WindowMessage.CREATE:
					CreateStruct cs = (CreateStruct)Marshal.PtrToStructure(lParam, typeof(CreateStruct));
					if (cs.hwndParent == IntPtr.Zero)
					{
						bounds.X = cs.x;
						bounds.Y = cs.y;
						bounds.Width = cs.cx;
						bounds.Height = cs.cy;

						Win32Rectangle rect;
						API.GetClientRect(handle, out rect);
						client_rectangle = rect.ToRectangle();

						invisible_since_creation = true;
					}
					break;

				case WindowMessage.CLOSE:
					System.ComponentModel.CancelEventArgs e = new System.ComponentModel.CancelEventArgs();

					if (Closing != null)
						Closing(this, e);

					if (!e.Cancel) {
						DestroyWindow();
						break;
					}

					return IntPtr.Zero;

				case WindowMessage.DESTROY:
					exists = false;

					API.UnregisterClass(ClassName, Instance);
					window.Dispose();

					if (Closed != null)
						Closed(this, EventArgs.Empty);

					break;

					#endregion
			}
			return API.DefWindowProc(handle, message, wParam, lParam);
		}

		private void EnableMouseTracking() {
			TrackMouseEventStructure me = new TrackMouseEventStructure();
			me.Size = TrackMouseEventStructure.SizeInBytes;
			me.TrackWindowHandle = window.handle;
			me.Flags = TrackMouseEventFlags.LEAVE;

			if (!API.TrackMouseEvent(ref me))
				Debug.Print("[Warning] Failed to enable mouse tracking, error: {0}.",
				            Marshal.GetLastWin32Error());
		}

		IntPtr CreateWindow(int x, int y, int width, int height, string title, DisplayDevice device) {
			// Use win32 to create the native window.
			// Keep in mind that some construction code runs in the WM_CREATE message handler.

			// The style of a parent window is different than that of a child window.
			// Note: the child window should always be visible, even if the parent isn't.
			WindowStyle style = WindowStyle.OverlappedWindow | WindowStyle.ClipChildren;
			ExtendedWindowStyle ex_style = ParentStyleEx;

			// Find out the final window rectangle, after the WM has added its chrome (titlebar, sidebars etc).
			Win32Rectangle rect = new Win32Rectangle();
			rect.left = x; rect.top = y; rect.right = x + width; rect.bottom = y + height;
			API.AdjustWindowRectEx(ref rect, style, false, ex_style);

			// Create the window class that we will use for this window.
			// The current approach is to register a new class for each top-level WinGLWindow we create.
			if (!class_registered) {
				ExtendedWindowClass wc = new ExtendedWindowClass();
				wc.Size = ExtendedWindowClass.SizeInBytes;
				wc.Style = ClassStyle.OwnDC;
				wc.Instance = Instance;
				wc.WndProc = WindowProcedureDelegate;
				wc.ClassName = ClassName;
				wc.Icon = Icon != null ? Icon.Handle : IntPtr.Zero;
				#warning "This seems to resize one of the 'large' icons, rather than using a small icon directly (multi-icon files). Investigate!"
				wc.IconSm = Icon != null ? new Icon(Icon, 16, 16).Handle : IntPtr.Zero;
				wc.Cursor = API.LoadCursor(IntPtr.Zero, (IntPtr)32512); // CursorName.Arrow
				ushort atom = API.RegisterClassEx(ref wc);

				if (atom == 0)
					throw new PlatformException("Failed to register window class. Error: " + Marshal.GetLastWin32Error());

				class_registered = true;
			}

			IntPtr window_name = Marshal.StringToHGlobalAuto(title);
			IntPtr handle = API.CreateWindowEx(
				ex_style, ClassName, window_name, style,
				rect.left, rect.top, rect.Width, rect.Height,
				IntPtr.Zero, IntPtr.Zero, Instance, IntPtr.Zero);

			if (handle == IntPtr.Zero)
				throw new PlatformException("Failed to create window. Error: " + Marshal.GetLastWin32Error());

			return handle;
		}

		/// <summary> Starts the teardown sequence for the current window. </summary>
		void DestroyWindow() {
			if (exists) {
				Debug.Print("Destroying window: {0}", window.handle);
				API.DestroyWindow(window.handle);
				exists = false;
			}
		}

		void SetHiddenBorder( bool hidden ) {
			suppress_resize++;
			HiddenBorder = hidden;
			ProcessEvents();
			suppress_resize--;
		}

		void ResetWindowState() {
			suppress_resize++;
			WindowState = WindowState.Normal;
			ProcessEvents();
			suppress_resize--;
		}
		
		const uint GMEM_MOVEABLE = 2;
		const uint CF_UNICODETEXT = 13, CF_TEXT = 1;
		public unsafe string GetClipboardText() {
			// retry up to 10 times
			for (int i = 0; i < 10; i++) {
				if (!API.OpenClipboard(window.handle)) {
					Thread.Sleep(100);
					continue;
				}
				
				bool isUnicode = true;
				IntPtr hGlobal = API.GetClipboardData(CF_UNICODETEXT);
				if (hGlobal == IntPtr.Zero) {
					hGlobal = API.GetClipboardData(CF_TEXT);
					isUnicode = false;
				}
				if (hGlobal == IntPtr.Zero) { API.CloseClipboard(); return ""; }
				
				IntPtr src = API.GlobalLock(hGlobal);
				string value = isUnicode ? new String((char*)src) : new String((sbyte*)src);
				API.GlobalUnlock(hGlobal);
				
				API.CloseClipboard();
				return value;
			}
			return "";
		}
		
		public unsafe void SetClipboardText( string value ) {
			UIntPtr dstSize = (UIntPtr)((value.Length + 1) * Marshal.SystemDefaultCharSize);
			// retry up to 10 times
			for (int i = 0; i < 10; i++) {
				if (!API.OpenClipboard(window.handle)) {
					Thread.Sleep(100);
					continue;
				}
				
				IntPtr hGlobal = API.GlobalAlloc(GMEM_MOVEABLE, dstSize);
				if (hGlobal == IntPtr.Zero) { API.CloseClipboard(); return; }
				
				IntPtr dst = API.GlobalLock(hGlobal);
				fixed (char* src = value) {
					CopyString_Unicode((IntPtr)src, dst, value.Length);
				}
				API.GlobalUnlock(hGlobal);
				
				API.EmptyClipboard();
				API.SetClipboardData(CF_UNICODETEXT, hGlobal);
				API.CloseClipboard();
				return;
			}
		}
		
		unsafe static void CopyString_Unicode(IntPtr src, IntPtr dst, int numChars) {
			char* src2 = (char*)src, dst2 = (char*)dst;
			for (int i = 0; i < numChars; i++) { dst2[i] = src2[i]; }
			dst2[numChars] = '\0';
		}

		public Rectangle Bounds {
			get { return bounds; }
			set {
				// Note: the bounds variable is updated when the resize/move message arrives.
				API.SetWindowPos(window.handle, IntPtr.Zero, value.X, value.Y, value.Width, value.Height, 0);
			}
		}

		public Point Location {
			get { return Bounds.Location; }
			set {
				// Note: the bounds variable is updated when the resize/move message arrives.
				API.SetWindowPos(window.handle, IntPtr.Zero, value.X, value.Y, 0, 0, SetWindowPosFlags.NOSIZE);
			}
		}

		public Size Size {
			get { return Bounds.Size; }
			set {
				// Note: the bounds variable is updated when the resize/move message arrives.
				API.SetWindowPos(window.handle, IntPtr.Zero, 0, 0, value.Width, value.Height, SetWindowPosFlags.NOMOVE);
			}
		}

		public Rectangle ClientRectangle {
			get {
				if (client_rectangle.Width == 0)
					client_rectangle.Width = 1;
				if (client_rectangle.Height == 0)
					client_rectangle.Height = 1;
				return client_rectangle;
			} set {
				ClientSize = value.Size;
			}
		}

		public Size ClientSize {
			get { return ClientRectangle.Size; }
			set {
				WindowStyle style = (WindowStyle)API.GetWindowLong(window.handle, GetWindowLongOffsets.STYLE);
				Win32Rectangle rect = Win32Rectangle.From(value);
				API.AdjustWindowRect(ref rect, style, false);
				Size = new Size(rect.Width, rect.Height);
			}
		}

		public Icon Icon {
			get { return icon; }
			set {
				icon = value;
				if (window.handle != IntPtr.Zero)
				{
					//Icon small = new Icon( value, 16, 16 );
					//GC.KeepAlive( small );
					API.SendMessage(window.handle, WindowMessage.SETICON, (IntPtr)0, icon == null ? IntPtr.Zero : value.Handle);
					API.SendMessage(window.handle, WindowMessage.SETICON, (IntPtr)1, icon == null ? IntPtr.Zero : value.Handle);
				}
			}
		}

		public bool Focused {
			get { return focused; }
		}

		public bool Visible {
			get { return API.IsWindowVisible(window.handle); }
			set {
				if (value) {
					API.ShowWindow(window.handle, ShowWindowCommand.SHOW);
					if (invisible_since_creation) {
						API.BringWindowToTop(window.handle);
						API.SetForegroundWindow(window.handle);
					}
				} else {
					API.ShowWindow(window.handle, ShowWindowCommand.HIDE);
				}
			}
		}

		public bool Exists { get { return exists; } }

		public void Close() {
			API.PostMessage(window.handle, WindowMessage.CLOSE, IntPtr.Zero, IntPtr.Zero);
		}

		public WindowState WindowState {
			get { return windowState; }
			set {
				if (WindowState == value)
					return;

				ShowWindowCommand command = 0;
				bool exiting_fullscreen = false;

				switch (value) {
					case WindowState.Normal:
						command = ShowWindowCommand.RESTORE;

						// If we are leaving fullscreen mode we need to restore the border.
						if (WindowState == WindowState.Fullscreen)
							exiting_fullscreen = true;
						break;

					case WindowState.Maximized:
						// Reset state to avoid strange interactions with fullscreen/minimized windows.
						ResetWindowState();
						command = ShowWindowCommand.MAXIMIZE;
						break;

					case WindowState.Minimized:
						command = ShowWindowCommand.MINIMIZE;
						break;

					case WindowState.Fullscreen:
						// We achieve fullscreen by hiding the window border and sending the MAXIMIZE command.
						// We cannot use the WindowState.Maximized directly, as that will not send the MAXIMIZE
						// command for windows with hidden borders.

						// Reset state to avoid strange side-effects from maximized/minimized windows.
						ResetWindowState();
						previous_bounds = Bounds;
						SetHiddenBorder( true );
						
						command = ShowWindowCommand.MAXIMIZE;
						API.SetForegroundWindow(window.handle);
						break;
				}

				if( command != 0 )
					API.ShowWindow(window.handle, command);

				// Restore previous window border or apply pending border change when leaving fullscreen mode.
				if( exiting_fullscreen )
					SetHiddenBorder( false );

				// Restore previous window size/location if necessary
				if (command == ShowWindowCommand.RESTORE && previous_bounds != Rectangle.Empty) {
					Bounds = previous_bounds;
					previous_bounds = Rectangle.Empty;
				}
			}
		}

		bool hiddenBorder;
		bool HiddenBorder {
			set {
				if( hiddenBorder == value ) return;

				// We wish to avoid making an invisible window visible just to change the border.
				// However, it's a good idea to make a visible window invisible temporarily, to
				// avoid garbage caused by the border change.
				bool was_visible = Visible;

				// To ensure maximized/minimized windows work correctly, reset state to normal,
				// change the border, then go back to maximized/minimized.
				WindowState state = WindowState;
				ResetWindowState();
				WindowStyle style = WindowStyle.ClipChildren | WindowStyle.ClipSiblings;
				style |= (value ? WindowStyle.Popup : WindowStyle.OverlappedWindow);

				// Make sure client size doesn't change when changing the border style.
				Win32Rectangle rect = Win32Rectangle.From( bounds );
				API.AdjustWindowRectEx( ref rect, style, false, ParentStyleEx );

				// This avoids leaving garbage on the background window.
				if( was_visible )
					Visible = false;

				API.SetWindowLong(window.handle, GetWindowLongOffsets.STYLE, (int)style);
				API.SetWindowPos(window.handle, IntPtr.Zero, 0, 0, rect.Width, rect.Height,
				                 SetWindowPosFlags.NOMOVE | SetWindowPosFlags.NOZORDER |
				                 SetWindowPosFlags.FRAMECHANGED);

				// Force window to redraw update its borders, but only if it's
				// already visible (invisible windows will change borders when
				// they become visible, so no need to make them visiable prematurely).
				if ( was_visible )
					Visible = true;
				WindowState = state;
			}
		}

		public Point PointToClient(Point point) {
			if (!API.ScreenToClient(window.handle, ref point))
				throw new InvalidOperationException(String.Format(
					"Could not convert point {0} from client to screen coordinates. Windows error: {1}",
					point.ToString(), Marshal.GetLastWin32Error()));

			return point;
		}

		public Point PointToScreen(Point p) {
			throw new NotImplementedException();
		}

		public event EventHandler Move;
		public event EventHandler Resize;
		public event EventHandler<System.ComponentModel.CancelEventArgs> Closing;
		public event EventHandler Closed;
		public event EventHandler Disposed;
		public event EventHandler IconChanged;
		public event EventHandler ClientSizeChanged;
		public event EventHandler VisibleChanged;
		public event EventHandler FocusedChanged;
		public event EventHandler WindowStateChanged;
		public event EventHandler<KeyPressEventArgs> KeyPress;
		public event EventHandler MouseEnter;
		public event EventHandler MouseLeave;

		MSG msg;
		public void ProcessEvents() {
			while( API.PeekMessage(ref msg, IntPtr.Zero, 0, 0, 1) ) {
				API.TranslateMessage(ref msg);
				API.DispatchMessage(ref msg);
			}
			IntPtr foreground = API.GetForegroundWindow();
			if( foreground != IntPtr.Zero )
				focused = foreground == window.handle;
		}

		public IWindowInfo WindowInfo {
			get { return window; }
		}
		
		public KeyboardDevice Keyboard {
			get { return keyboard; }
		}
		
		public MouseDevice Mouse {
			get { return mouse; }
		}

		public Point DesktopCursorPos {
			get {
				POINT pos = default( POINT );
				API.GetCursorPos( ref pos );
				return new Point( pos.X, pos.Y );
			}
			set { API.SetCursorPos( value.X, value.Y ); }
		}
		
		bool cursorVisible = true;
		public bool CursorVisible {
			get { return cursorVisible; }
			set {
				cursorVisible = value;
				API.ShowCursor( value ? 1 : 0 );
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		void Dispose(bool calledManually) {
			if (!disposed) {
				if (calledManually) {
					// Safe to clean managed resources
					DestroyWindow();
					if (Icon != null)
						Icon.Dispose();
				} else {
					Debug.Print("[Warning] INativeWindow leaked ({0}). Did you forget to call INativeWindow.Dispose()?", this);
				}
				disposed = true;
			}
		}

		~WinGLNative() {
			Dispose(false);
		}
	}
}
