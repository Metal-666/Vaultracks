using SQLite;

using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Vaultracks;

public static class DatabaseManager {

	public const string DataDirectory = "/data";

	public static ConcurrentDictionary<UserAuth, SQLiteAsyncConnection> ActiveConnections { get; } =
		new();

	public static async Task<SQLiteAsyncConnection?> GetDb(UserAuth userAuth, bool createIfDoesNotExist) {

		try {

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

					return null;

				}

			}

			return db;

		}

		catch {

			return null;

		}

	}

	public static string GetDatabaseFilePath(string username) =>
		Path.Combine(DataDirectory, $"{username}.db");

}