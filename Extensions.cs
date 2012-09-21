using System.IO;

namespace UnityVS
{
	static class Extensions
	{
		public static void WriteTo(this Stream stream, Stream dest)
		{
			var buffer = new byte[8 * 1024];
			int length;

			while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
				dest.Write(buffer, 0, length);
		}
	}
}