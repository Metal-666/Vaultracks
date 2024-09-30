using System;
using System.Net;
using System.Text;

namespace Vaultracks;

public record UserAuth(string Username, string DatabaseKey) {

	public static UserAuth? ParseUrlEncoded(string username, string databaseKey) {

		(string? Username, string? DatabaseKey) = (WebUtility.UrlDecode(username), WebUtility.UrlDecode(databaseKey));

		if(Username == null) {

			return null;

		}

		return new(Username, DatabaseKey ?? "");

	}

	public static UserAuth? ParseBase64(string base64auth) {

		const string BasicAuthPrefix = "Basic ";

		if(!base64auth.StartsWith(BasicAuthPrefix)) {

			return null;

		}

		string[] auth =
			Encoding.UTF8
					.GetString(Convert.FromBase64String(base64auth[BasicAuthPrefix.Length..]))
					.Split(':');

		return new(auth[0], auth[1]);

	}

}