
using System;
using System.IO;
using System.Text;
using GitSharp.Core.Exceptions;

namespace GitSharp.Core
{
	
	/// <summary>
	/// Decodes a Git object.
	/// </summary>
	public class ObjectDecoder : IDisposable
	{
		private byte[] raw;
		private string line;
		private StreamReader reader;
		
		public ObjectDecoder(byte[] raw)
		{
			this.raw = raw;
		}
		
		public string TryRead(string field)
		{
			string val;
			
			if (reader == null)
			{
				reader = new StreamReader(new MemoryStream(raw));
				line = reader.ReadLine();
			}
			
			if (line == null || !line.StartsWith(field + " "))
			{
				return null;
			}
			
			val = line.Substring((field + " ").Length);
			
			line = reader.ReadLine();
			
			return val;
		}
		
		public String Read(string field)
		{
			string val = TryRead(field);
			
			if (val == null)
			{
				throw new CorruptObjectException("no " + field);
			}
			
			return val;
		}
		
		public string ReadToEnd()
		{
			return reader.ReadToEnd();
		}
		
		public void Dispose()
		{
			reader.Dispose();
		}
	}
}
