using System.Collections.Generic;
using System.Text;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Builds the JS response pages that update strip iframes in the view page.
	/// Loaded inside the hidden cmd iframe; targets parent strip iframes.
	/// </summary>
	static class StripUpdater
	{
		/// <summary>
		/// Builds a JS page that navigates only the iframes of changed strips.
		/// </summary>
		public static string BuildPartialUpdate(string key, StripSet strips, List<int> changed)
		{
			var sb = new StringBuilder();
			sb.Append("<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">");

			foreach (int i in changed)
			{
				string url = "http://" + DimensionProbe.MagicHost +
				             "/strip-frame?key=" + key +
				             "&i=" + i +
				             "&r=" + strips.Strips[i].Revision;
				sb.Append("if(parent.frames['strip" + i + "'])parent.frames['strip" + i + "'].location='" + url + "';");
			}

			sb.Append("</SCRIPT></BODY></HTML>");
			return sb.ToString();
		}

		/// <summary>
		/// Builds a JS page that updates only the strips currently visible in the driver
		/// viewport, based on the scroll position synced from the client.
		/// </summary>
		public static string BuildViewportUpdate(string key, StripSet strips)
		{
			int scrollY     = strips.LastScrollY;
			int vpHeight    = strips.ViewportHeight > 0 ? strips.ViewportHeight : strips.ImageHeight;
			int firstStrip  = scrollY / strips.StripHeight;
			int lastStrip   = (scrollY + vpHeight) / strips.StripHeight;

			firstStrip = System.Math.Max(0, firstStrip);
			lastStrip  = System.Math.Min(strips.Strips.Length - 1, lastStrip);

			var sb = new StringBuilder();
			sb.Append("<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">");

			for (int i = firstStrip; i <= lastStrip; i++)
			{
				string url = "http://" + DimensionProbe.MagicHost +
				             "/strip-frame?key=" + key +
				             "&i=" + i +
				             "&r=" + strips.Strips[i].Revision;
				sb.Append("if(parent.frames['strip" + i + "'])parent.frames['strip" + i + "'].location='" + url + "';");
			}

			sb.Append("</SCRIPT></BODY></HTML>");
			return sb.ToString();
		}

		/// <summary>
		/// Builds a JS page that updates every strip iframe regardless of visibility.
		/// </summary>
		public static string BuildFullUpdate(string key, StripSet strips)
		{
			var changed = new List<int>(strips.Strips.Length);
			for (int i = 0; i < strips.Strips.Length; i++)
				changed.Add(i);
			return BuildPartialUpdate(key, strips, changed);
		}

		/// <summary>
		/// Builds a JS page that forces the parent (view) to reload its full content.
		/// Used when the page height changed and strip iframes need to be recreated.
		/// </summary>
		public static string BuildViewReload(string key)
		{
			return
				"<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">" +
				"parent.location=parent.location.href;" +
				"</SCRIPT></BODY></HTML>";
		}
	}
}
