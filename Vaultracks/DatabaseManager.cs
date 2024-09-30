using SQLite;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Vaultracks;

public static class DatabaseManager {

	public const string DataDirectory = "/data";

	public static ConcurrentDictionary<UserAuth, SQLiteAsyncConnection> ActiveConnections { get; } =
		new();

	public static IEnumerable<string> ListDbs() =>
		Directory.EnumerateFiles(DataDirectory, "*.db")
					.Select(filePath =>
										Path.GetFileName(filePath));

	/// <exception cref="Exception"></exception>
	public static async Task<SQLiteAsyncConnection> GetDb(UserAuth userAuth, bool createIfDoesNotExist) {

		if(!ActiveConnections.TryGetValue(userAuth, out SQLiteAsyncConnection? db)) {

			SQLiteOpenFlags flags =
				SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.FullMutex;

			if(createIfDoesNotExist) {

				flags |= SQLiteOpenFlags.Create;

			}

			db = new(new SQLiteConnectionString(GetDatabaseFilePath(userAuth.Username),
																				flags,
																				true,
																				userAuth.DatabaseKey));

			await db.CreateTableAsync<Location>();

			if(!ActiveConnections.TryAdd(userAuth, db)) {

				await db.CloseAsync();

				throw new Exception("New connection could not be added to the Concurrent Dictionary");

			}

		}

		return db;

	}

	public static string GetDatabaseFilePath(string username) =>
		Path.Combine(DataDirectory, $"{username}.db");

}