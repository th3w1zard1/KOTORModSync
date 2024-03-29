﻿// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace KOTORModSync.Core
{
	public class ConsoleConfig
	{
		private const uint ENABLE_MOUSE_INPUT = 0x0010;
		private const uint ENABLE_QUICK_EDIT = 0x0040;

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
		
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
		
		public enum CtrlTypes
		{
			CTRL_CLOSE_EVENT = 2
		}
		public delegate bool HandlerRoutine(CtrlTypes CtrlType);
		
		private const int MF_BYCOMMAND = 0x00000000;
		public const int SC_CLOSE = 0xF060;

		[DllImport("user32.dll")]
		public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		[DllImport("kernel32.dll", ExactSpelling = true)]
		private static extern IntPtr GetConsoleWindow();

		private static IntPtr hSysMenu;

		public static void DisableConsoleCloseButton() => DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_BYCOMMAND);
		
		public static void EnableCloseButton()
		{
			// Reset the system menu to its default state, which includes the close button
			GetSystemMenu(GetConsoleWindow(), true);
			// Need to call GetSystemMenu again to update the handle
			hSysMenu = GetSystemMenu(GetConsoleWindow(), false);
		}

		public static void DisableQuickEdit()
		{
			try
			{
				IntPtr consoleHandle = GetStdHandle(-10); // STD_INPUT_HANDLE
				if ( !GetConsoleMode(consoleHandle, out uint consoleMode) )
				{
					Logger.LogWarning("Could not get current console mode. You can ignore this warning if you're piping your terminal to something other than cmd.exe and powershell.");
					return;
				}

				consoleMode &= ~ENABLE_QUICK_EDIT;

				if ( !SetConsoleMode(consoleHandle, consoleMode) )
				{
					Logger.LogError("Could not set console mode on console handle");
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CONSOLE_SCREEN_BUFFER_INFOEX
		{
			public uint cbSize;
			public COORD dwSize;
			public COORD dwCursorPosition;
			public ushort wAttributes;
			public SMALL_RECT srWindow;
			public COORD dwMaximumWindowSize;
			public ushort wPopupAttributes;
			public bool bFullscreenSupported;
			public COLORREF Black;
			public COLORREF DarkBlue;
			public COLORREF DarkGreen;
			public COLORREF DarkCyan;
			public COLORREF DarkRed;
			public COLORREF DarkMagenta;
			public COLORREF DarkYellow;
			public COLORREF Gray;
			public COLORREF DarkGray;
			public COLORREF Blue;
			public COLORREF Green;
			public COLORREF Cyan;
			public COLORREF Red;
			public COLORREF Magenta;
			public COLORREF Yellow;
			public COLORREF White;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct COORD
		{
			public short X;
			public short Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SMALL_RECT
		{
			public short Left;
			public short Top;
			public short Right;
			public short Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct COLORREF
		{
			public uint ColorDWORD;

			public COLORREF(Color color) =>
				ColorDWORD = color.R + ((uint)color.G << 8) + ((uint)color.B << 16);

			public Color GetSystemColor() =>
				Color.FromArgb(
					(int)(0x000000FFU & ColorDWORD),
					(int)(0x0000FF00U & ColorDWORD) >> 8,
					(int)(0x00FF0000U & ColorDWORD) >> 16
				);
		}
	}
}
