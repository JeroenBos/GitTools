using JBSnorro.Diagnostics;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Threading;
using static CI.UI.NotificationIconContextMenuItems;

namespace CI.UI
{
	sealed class NotificationIconContextMenus
	{
		private static MenuItem exitMenuItem => new MenuItem(GetMenuItemCaption(Exit), (sender, e) => Dispatcher.CurrentDispatcher.InvokeShutdown());
		private static MenuItem cancelMenuItem(NotificationIcon icon) => new MenuItem(GetMenuItemCaption(Cancel), (sender, e) => icon.RequestCancellation());
		private static MenuItem disregardTestResultsMenuItem(NotificationIcon icon) => new MenuItem(GetMenuItemCaption(DisregardTestResults), (sender, e) => ParentFailedTracker.RedoCanceledMessagesBecauseParentFailed(icon));

		public static ContextMenu Create(NotificationIconContextMenuItems items, NotificationIcon icon)
		{
			switch (items)
			{
				case None:
					return new ContextMenu();
				case Exit:
					return new ContextMenu(new[] { exitMenuItem });
				case Exit | Cancel:
					return new ContextMenu(new[] { cancelMenuItem(icon), exitMenuItem });
				case Exit | DisregardTestResults:
					return new ContextMenu(new[] { exitMenuItem, disregardTestResultsMenuItem(icon) });
				case Exit | Cancel | DisregardTestResults:
					return new ContextMenu(new[] { cancelMenuItem(icon), exitMenuItem, disregardTestResultsMenuItem(icon) });
				default:
					throw new DefaultSwitchCaseUnreachableException();
			}
		}

		public static NotificationIconContextMenuItems From(ContextMenu menu)
		{
			if (menu == null)
				return None;

			var result = menu.MenuItems
							 .Cast<MenuItem>()
							 .Select(menuItem => menuItem.Text)
							 .Select(ToMenuItemFlag)
							 .Aggregate((a, b) => a | b);
			return result;
		}

		private static NotificationIconContextMenuItems ToMenuItemFlag(string s)
		{
			foreach (var flag in Enum.GetValues(typeof(NotificationIconContextMenuItems)).Cast<NotificationIconContextMenuItems>())
			{
				string caption = GetMenuItemCaption(flag);
				if (caption == s)
					return flag;
			}
			throw new DefaultSwitchCaseUnreachableException();
		}
		private static string GetMenuItemCaption(NotificationIconContextMenuItems flag)
		{
			switch (flag)
			{
				case None:
					return "None";
				case Exit:
					return "Exit";
				case Cancel:
					return "Cancel";
				case DisregardTestResults:
					return "Dismiss parent failed";
				default:
					throw new DefaultSwitchCaseUnreachableException();
			}
		}
	}
	[Flags]
	internal enum NotificationIconContextMenuItems
	{
		None = 0,
		Exit = 1,
		Cancel = 2,
		DisregardTestResults = 4
	}
}
