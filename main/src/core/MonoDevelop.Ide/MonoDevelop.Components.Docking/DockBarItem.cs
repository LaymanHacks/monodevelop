//
// DockBarItem.cs
//
// Author:
//   Lluis Sanchez Gual
//

//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using Gtk;
using Mono.TextEditor;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.Components.Docking
{
	class DockBarItem: EventBox
	{
		DockBar bar;
		DockItem it;
		Box box;
		Label label;
		Alignment mainBox;
		AutoHideBox autoShowFrame;
		AutoHideBox hiddenFrame;
		uint autoShowTimeout = uint.MaxValue;
		uint autoHideTimeout = uint.MaxValue;
		int size;
		Gdk.Size lastFrameSize;
		StateType state;

		public DockBarItem (DockBar bar, DockItem it, int size)
		{
			Events = Events | Gdk.EventMask.EnterNotifyMask | Gdk.EventMask.LeaveNotifyMask;
			this.size = size;
			this.bar = bar;
			this.it = it;
			VisibleWindow = false;
			UpdateTab ();
			lastFrameSize = bar.Frame.Allocation.Size;
			bar.Frame.SizeAllocated += HandleBarFrameSizeAllocated;
		}
		
		void HandleBarFrameSizeAllocated (object o, SizeAllocatedArgs args)
		{
			if (!lastFrameSize.Equals (args.Allocation.Size)) {
				lastFrameSize = args.Allocation.Size;
				if (autoShowFrame != null)
					bar.Frame.UpdateSize (bar, autoShowFrame);
			}
		}
		
		protected override void OnDestroyed ()
		{
			base.OnDestroyed ();
			bar.Frame.SizeAllocated -= HandleBarFrameSizeAllocated;
		}
		
		
		public void Close ()
		{
			UnscheduleAutoShow ();
			UnscheduleAutoHide ();
			AutoHide (false);
			bar.RemoveItem (this);
			Destroy ();
		}
		
		public int Size {
			get { return size; }
			set { size = value; }
		}
		
		public void UpdateTab ()
		{
			if (Child != null) {
				Widget w = Child;
				Remove (w);
				w.Destroy ();
			}
			
			mainBox = new Alignment (0,0,1,1);
			if (bar.Orientation == Gtk.Orientation.Horizontal) {
				box = new HBox ();
				if (bar.AlignToEnd)
					mainBox.SetPadding (3, 3, 11, 9);
				else
					mainBox.SetPadding (3, 3, 9, 11);
			}
			else {
				box = new VBox ();
				if (bar.AlignToEnd)
					mainBox.SetPadding (11, 9, 3, 3);
				else
					mainBox.SetPadding (9, 11, 3, 3);
			}
			
			Gtk.Widget customLabel = null;
			if (it.DockLabelProvider != null)
				customLabel = it.DockLabelProvider.CreateLabel (bar.Orientation);
			
			if (customLabel != null) {
				customLabel.ShowAll ();
				box.PackStart (customLabel, true, true, 0);
			}
			else {
				if (it.Icon != null)
					box.PackStart (new Gtk.Image (it.Icon), false, false, 0);
					
				if (!string.IsNullOrEmpty (it.Label)) {
					label = new Gtk.Label (it.Label);
					label.UseMarkup = true;
					if (bar.Orientation == Gtk.Orientation.Vertical)
						label.Angle = 270;
					box.PackStart (label, true, true, 0);
				} else
					label = null;
			}

			box.Spacing = 2;
			mainBox.Add (box);
			mainBox.ShowAll ();
			Add (mainBox);
			state = StateType.Normal;
			QueueDraw ();
		}
		
		public MonoDevelop.Components.Docking.DockItem DockItem {
			get {
				return it;
			}
		}

		protected override void OnHidden ()
		{
			base.OnHidden ();
			UnscheduleAutoShow ();
			UnscheduleAutoHide ();
			AutoHide (false);
		}

		protected override bool OnExposeEvent (Gdk.EventExpose evnt)
		{
			var siblings = ((Gtk.Container)Parent).Children;
			using (var ctx = Gdk.CairoHelper.Create (evnt.Window)) {
				var alloc = Allocation;

				if (siblings[siblings.Length - 1] == this && !bar.AlignToEnd) {
					// Final light shadow
					if (bar.Orientation == Orientation.Vertical) {
						ctx.MoveTo (alloc.X, alloc.Y + alloc.Height - 0.5);
						ctx.RelLineTo (Allocation.Width, 0);
						ctx.Color = Styles.DockBarSeparatorColorLight;
						ctx.Stroke ();
						alloc.Height--;
					} else {
						ctx.MoveTo (alloc.X + alloc.Width - 0.5, alloc.Y);
						ctx.RelLineTo (0, Allocation.Height);
						ctx.Color = Styles.DockBarSeparatorColorLight;
						ctx.Stroke ();
						alloc.Width--;
					}
				}
				else if (siblings[0] == this && bar.AlignToEnd) {
					// Initial dark shadow
					if (bar.Orientation == Orientation.Vertical) {
						ctx.MoveTo (alloc.X, alloc.Y + 0.5);
						ctx.RelLineTo (Allocation.Width, 0);
						ctx.Color = Styles.DockBarSeparatorColorDark;
						ctx.Stroke ();
						alloc.Height--;
						alloc.Y++;
					} else {
						ctx.MoveTo (alloc.X + 0.5, alloc.Y);
						ctx.RelLineTo (0, Allocation.Height);
						ctx.Color = Styles.DockBarSeparatorColorDark;
						ctx.Stroke ();
						alloc.Width--;
						alloc.X++;
					}
				}

				ctx.LineWidth = 1;
				var fillArea = alloc;

				// Light shadow

				if (bar.Orientation == Orientation.Vertical) {
					var y = alloc.Y + 0.5;
					ctx.MoveTo (alloc.X, y);
					ctx.RelLineTo (Allocation.Width, 0);
					ctx.Color = Styles.DockBarSeparatorColorLight;
					ctx.Stroke ();
				} else {
					var x = alloc.X + 0.5;
					ctx.MoveTo (x, alloc.Y);
					ctx.RelLineTo (0, Allocation.Height);
					ctx.Color = Styles.DockBarSeparatorColorLight;
					ctx.Stroke ();
				}


				// Background
				if (state == StateType.Prelight) {
					ctx.Rectangle (fillArea.X, fillArea.Y, fillArea.Width, fillArea.Height);
					ctx.Color = Styles.IncreaseLight (Styles.DockBarBackground1, 0.5);
					ctx.Fill ();
				} else if (state == StateType.Selected) {
					ctx.Rectangle (fillArea.X, fillArea.Y, fillArea.Width, fillArea.Height);
					var gr = bar.Orientation == Orientation.Vertical ? 
						new Cairo.LinearGradient (fillArea.X, fillArea.Y, fillArea.X + fillArea.Width, fillArea.Y) :
							new Cairo.LinearGradient (fillArea.X, fillArea.Y, fillArea.X, fillArea.Y + fillArea.Height);
					gr.AddColorStop (0, Styles.Shift (Styles.DockBarBackground1, 0.9));
					gr.AddColorStop (1, Styles.Shift (Styles.DockBarBackground1, 0.725));
					ctx.Pattern = gr;
					ctx.Fill ();
				}

				// Dark shadow

				if (bar.Orientation == Orientation.Vertical) {
					var y = alloc.Y + alloc.Height - 0.5;
					ctx.MoveTo (alloc.X, y);
					ctx.RelLineTo (Allocation.Width, 0);
					ctx.Color = Styles.DockBarSeparatorColorDark;
					ctx.Stroke ();
				} else {
					var x = alloc.X + alloc.Width - 0.5;
					ctx.MoveTo (x, alloc.Y);
					ctx.RelLineTo (0, Allocation.Height);
					ctx.Color = Styles.DockBarSeparatorColorDark;
					ctx.Stroke ();
				}
			}
			bool res = base.OnExposeEvent (evnt);
			return res;
		}

		public void Present (bool giveFocus)
		{
			AutoShow ();
			if (giveFocus) {
				GLib.Timeout.Add (200, delegate {
					// Using a small delay because AutoShow uses an animation and setting focus may
					// not work until the item is visible
					it.SetFocus ();
					ScheduleAutoHide (false);
					return false;
				});
			}
		}

		public void Minimize ()
		{
			AutoHide (false);
		}

		void AutoShow ()
		{
			UnscheduleAutoHide ();
			if (autoShowFrame == null) {
				if (hiddenFrame != null)
					bar.Frame.AutoHide (it, hiddenFrame, false);
				autoShowFrame = bar.Frame.AutoShow (it, bar, size);
				autoShowFrame.EnterNotifyEvent += OnFrameEnter;
				autoShowFrame.LeaveNotifyEvent += OnFrameLeave;
				autoShowFrame.KeyPressEvent += OnFrameKeyPress;
				state = StateType.Selected;
				QueueDraw ();
			}
		}
		
		void AutoHide (bool animate)
		{
			UnscheduleAutoShow ();
			if (autoShowFrame != null) {
				size = autoShowFrame.Size;
				hiddenFrame = autoShowFrame;
				autoShowFrame.Hidden += delegate {
					hiddenFrame = null;
				};
				bar.Frame.AutoHide (it, autoShowFrame, animate);
				autoShowFrame.EnterNotifyEvent -= OnFrameEnter;
				autoShowFrame.LeaveNotifyEvent -= OnFrameLeave;
				autoShowFrame.KeyPressEvent -= OnFrameKeyPress;
				autoShowFrame = null;
				state = StateType.Normal;
				QueueDraw ();
			}
		}
		
		void ScheduleAutoShow ()
		{
			UnscheduleAutoHide ();
			if (autoShowTimeout == uint.MaxValue) {
				autoShowTimeout = GLib.Timeout.Add (bar.Frame.AutoShowDelay, delegate {
					autoShowTimeout = uint.MaxValue;
					AutoShow ();
					return false;
				});
			}
		}
		
		void ScheduleAutoHide (bool cancelAutoShow)
		{
			ScheduleAutoHide (cancelAutoShow, false);
		}
		
		void ScheduleAutoHide (bool cancelAutoShow, bool force)
		{
			if (cancelAutoShow)
				UnscheduleAutoShow ();
			if (force)
				it.Widget.FocusChild = null;
			if (autoHideTimeout == uint.MaxValue) {
				autoHideTimeout = GLib.Timeout.Add (force ? 0 : bar.Frame.AutoHideDelay, delegate {
					// Don't hide the item if it has the focus. Try again later.
					if (it.Widget.FocusChild != null && !force)
						return true;
					// Don't hide the item if the mouse pointer is still inside the window. Try again later.
					int px, py;
					it.Widget.GetPointer (out px, out py);
					if (it.Widget.Visible && it.Widget.IsRealized && it.Widget.Allocation.Contains (px, py) && !force)
						return true;
					autoHideTimeout = uint.MaxValue;
					AutoHide (true);
					return false;
				});
			}
		}
		
		void UnscheduleAutoShow ()
		{
			if (autoShowTimeout != uint.MaxValue) {
				GLib.Source.Remove (autoShowTimeout);
				autoShowTimeout = uint.MaxValue;
			}
		}
		
		void UnscheduleAutoHide ()
		{
			if (autoHideTimeout != uint.MaxValue) {
				GLib.Source.Remove (autoHideTimeout);
				autoHideTimeout = uint.MaxValue;
			}
		}
		
		protected override bool OnEnterNotifyEvent (Gdk.EventCrossing evnt)
		{
			ScheduleAutoShow ();
			state = StateType.Prelight;
			QueueDraw ();
			return base.OnEnterNotifyEvent (evnt);
		}
		
		protected override bool OnLeaveNotifyEvent (Gdk.EventCrossing evnt)
		{
			ScheduleAutoHide (true);
			if (autoShowFrame == null) {
				state = StateType.Normal;
				QueueDraw ();
			}
			return base.OnLeaveNotifyEvent (evnt);
		}
		
		void OnFrameEnter (object s, Gtk.EnterNotifyEventArgs args)
		{
			AutoShow ();
		}

		void OnFrameKeyPress (object s, Gtk.KeyPressEventArgs args)
		{
			if (args.Event.Key == Gdk.Key.Escape)
				ScheduleAutoHide (true, true);
		}
		
		void OnFrameLeave (object s, Gtk.LeaveNotifyEventArgs args)
		{
			if (args.Event.Detail != Gdk.NotifyType.Inferior)
				ScheduleAutoHide (true);
		}
		
		protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
		{
			if (evnt.TriggersContextMenu ()) {
				it.ShowDockPopupMenu (evnt.Time);
			} else if (evnt.Button == 1) {
				if (evnt.Type == Gdk.EventType.TwoButtonPress) {
					it.Status = DockItemStatus.Dockable;
				} else {
					AutoShow ();
					it.Present (true);
				}
			}
			return true;
		}
	}
}
