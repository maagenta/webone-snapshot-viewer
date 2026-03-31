using System.Text;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Generates the HTML pages of the snapshot viewer.
	///
	/// Layer 1 — Shell page (/snap): scrollable page containing strip iframes,
	///            cmd iframe, click overlay, and scroll script directly.
	///
	/// Layer 2 — Strip frame (/strip-frame): tiny HTML wrapper per strip image.
	///            Only reloads when its strip pixel data changed.
	/// </summary>
	static class SnapshotPage
	{
		/// <summary>
		/// Shell page. Contains strip iframes, cmd iframe, click overlay, and scroll script.
		/// The page itself scrolls — no nested view iframe.
		/// </summary>
		public static string GetShellPage(string sessionKey, StripSet strips)
		{
			string scriptUrl = "http://" + DimensionProbe.MagicHost + "/scroll.js?key=" + sessionKey;

			var sb = new StringBuilder();
			sb.Append("<HTML><HEAD><TITLE>Snapshot</TITLE>");
			sb.Append("<STYLE TYPE=\"text/css\">");
			sb.Append("HTML,BODY{margin:0;padding:0;background:#000;font-size:0;line-height:0}");
			sb.Append("IFRAME{display:block;width:100%;border:0;overflow:hidden;margin:0;padding:0;vertical-align:top}");
			sb.Append("</STYLE>");
			sb.Append("<SCRIPT LANGUAGE=\"JavaScript\">");
			sb.Append(IFrameManager.GetScript());
			sb.Append("</SCRIPT>");
			sb.Append("<SCRIPT LANGUAGE=\"JavaScript\" SRC=\"" + scriptUrl + "\"></SCRIPT>");
			sb.Append("</HEAD><BODY>");

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

			// Hidden cmd iframe — receives click/scroll responses and updates strip iframes via JS.
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
