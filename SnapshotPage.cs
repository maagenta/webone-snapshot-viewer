using System.Text;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Generates the HTML layers of the snapshot viewer.
	///
	/// Layer 1 — Shell page  (/snap):  outer page with one full-viewport iframe.
	///            URL never changes. Returned once and kept by the browser.
	///
	/// Layer 2 — View content (/view): stacks N strip iframes + a hidden cmd iframe.
	///            Strip iframes are updated individually; the view page itself never reloads.
	///
	/// Layer 3 — Strip frame (/strip-frame): tiny HTML wrapper per strip image.
	///            Only reloads when its strip pixel data changed.
	/// </summary>
	static class SnapshotPage
	{
		/// <summary>
		/// Outer shell page. Contains a single full-viewport iframe pointing to /view.
		/// </summary>
		public static string GetShellPage(string sessionKey, int imageHeight)
		{
			string viewUrl   = "http://" + DimensionProbe.MagicHost + "/view?key=" + sessionKey;
			string scriptUrl = "http://" + DimensionProbe.MagicHost + "/scroll.js?key=" + sessionKey;
			return
				"<HTML><HEAD><TITLE>Snapshot</TITLE>" +
				"<STYLE TYPE=\"text/css\">" +
				"HTML,BODY{margin:0;padding:0}" +
				"IFRAME{display:block;width:100%;border:0}" +
				"</STYLE>" +
				"<SCRIPT LANGUAGE=\"JavaScript\" SRC=\"" + scriptUrl + "\"></SCRIPT>" +
				"</HEAD><BODY>" +
				"<IFRAME SRC=\"" + viewUrl + "\" FRAMEBORDER=\"0\" SCROLLING=\"NO\" WIDTH=\"100%\" HEIGHT=\"" + imageHeight + "\">" +
				"</IFRAME>" +
				"</BODY></HTML>";
		}

		/// <summary>
		/// View content: one iframe per strip plus a hidden cmd iframe for click commands.
		/// Returned by /view on initial load. Never replaced as a whole after that —
		/// only individual strip iframes are updated via JS from the cmd iframe.
		/// </summary>
		public static string GetViewContent(string sessionKey, StripSet strips)
		{
			var sb = new StringBuilder();
			sb.Append("<HTML><HEAD><TITLE>Snapshot</TITLE>");
			sb.Append("<STYLE TYPE=\"text/css\">");
			sb.Append("HTML,BODY{margin:0;padding:0;background:#000;font-size:0;line-height:0}");
			sb.Append("IFRAME{display:block;width:100%;border:0;overflow:hidden;margin:0;padding:0;vertical-align:top}");
			sb.Append("</STYLE></HEAD><BODY>");

			for (int i = 0; i < strips.Strips.Length; i++)
			{
				int stripH = System.Math.Min(strips.StripHeight, strips.ImageHeight - i * strips.StripHeight);
				string src = "http://" + DimensionProbe.MagicHost +
				             "/strip-frame?key=" + sessionKey +
				             "&i=" + i +
				             "&r=" + strips.Strips[i].Revision;
				sb.Append(
					"<IFRAME NAME=\"strip" + i + "\"" +
					" SRC=\"" + src + "\"" +
					" HEIGHT=\"" + stripH + "\"" +
					" FRAMEBORDER=\"0\" SCROLLING=\"NO\" WIDTH=\"100%\"" +
					" MARGINWIDTH=\"0\" MARGINHEIGHT=\"0\">" +
					"</IFRAME>");
			}

			// Hidden cmd iframe — receives /click response which updates strip iframes via JS.
			sb.Append(
				"<IFRAME NAME=\"cmd\"" +
				" SRC=\"about:blank\"" +
				" FRAMEBORDER=\"0\" SCROLLING=\"NO\"" +
				" STYLE=\"position:absolute;left:-9999px;width:1px;height:1px\">" +
				"</IFRAME>");

			sb.Append(ClickHandler.GetClickScript(sessionKey, strips.ImageWidth, strips.ImageHeight));
			sb.Append("</BODY></HTML>");
			return sb.ToString();
		}

		/// <summary>
		/// Tiny HTML wrapper served inside each strip iframe.
		/// Contains a single image that fills the frame width.
		/// </summary>
		public static string GetStripFrame(string stripImageUrl)
		{
			return
				"<HTML><BODY STYLE=\"margin:0;padding:0;overflow:hidden;background:#000;line-height:0;font-size:0\">" +
				"<IMG SRC=\"" + stripImageUrl + "\" WIDTH=\"100%\" STYLE=\"display:block;vertical-align:top\"" +
				" ONERROR=\"var t=this;setTimeout(function(){t.src=t.src},800)\">" +
				"</BODY></HTML>";
		}
	}
}
