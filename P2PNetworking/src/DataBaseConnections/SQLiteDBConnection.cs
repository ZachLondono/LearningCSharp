using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace P2PNetworking {

	public class SQLiteDBConnection : IDBInterface  {
	
		private SqliteConnection DBConnection { get; }

		private SQLiteDBConnection(string storageDirectory) {
			// Verify that storage directory is a valid directory
			bool exists = System.IO.Directory.Exists(storageDirectory);
			if (!exists) throw new System.ArgumentException("Invalid storage directory, can't connect to database");
	
			DBConnection = new SqliteConnection($"Data Source={storageDirectory}/Peer2Peer.db");
			DBConnection.Open();
		}

		public static async Task<SQLiteDBConnection> CreateConnection(string storageDirectory) {
			SQLiteDBConnection connection = new SQLiteDBConnection(storageDirectory);

			await connection.CreateTableIfNotExist("peers", new string[]{"host VARCHAR(255)", "port INTEGER"});
			await connection.CreateTableIfNotExist("data", new string[]{"key BLOB", "value BLOB"});

			return connection;
		}

		public async Task CreateTableIfNotExist(string name, string[] columns) {
			var queryString = $"CREATE TABLE IF NOT EXISTS {name} \n(\n";
			for (int i = 0; i < columns.Length; i++) {
				queryString += "\t" + columns[i];
				if (i < columns.Length - 1) queryString += ",\n";
			}
			queryString += "\n);";

			var command = DBConnection.CreateCommand();
			command.CommandText = queryString;

			await Task.Run(() => command.ExecuteNonQuery());
		}

		public async Task<bool> ContainsKey(byte[] key) {
		
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT * FROM data WHERE key = $key";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
			
			bool exists = false;
			Task task = Task.Run(() => {
				using (var reader = command.ExecuteReader()) {
					exists = reader.HasRows; 
				}
			});

			await task;

			return exists;

		}

		public async Task<bool> InsertPair(DataPair pair) {

			// Inserts the key value pair, there should only be one instance of key
			// NOTE: this is likely susceptible to duplicate keys in the case of a race condition
			var command = DBConnection.CreateCommand();
			command.CommandText = @"
						INSERT INTO data(key, value)
						SELECT $key, $value
						WHERE NOT EXISTS (SELECT 1 FROM data WHERE key = $key) 
						";

			command.Parameters.Add("$key", SqliteType.Blob, pair.Key.Length).Value = pair.Key;
			command.Parameters.Add("$value", SqliteType.Blob, pair.Value.Length).Value = pair.Value;
			
			int rows = await Task<int>.Run(() => {
				return command.ExecuteNonQuery();
			});

			// insertion was successful if 1 row was changed
			return rows == 1;

		}

		public async Task<bool> UpdatePair(DataPair pair) {
	
			var command = DBConnection.CreateCommand();
			command.CommandText = @"
						UPDATE data
						SET value = $value
						WHERE key = $key;
						";
			command.Parameters.Add("$key", SqliteType.Blob, pair.Key.Length).Value = pair.Key;
			command.Parameters.Add("$value", SqliteType.Blob, pair.Value.Length).Value = pair.Value;		

			int rows = await Task<int>.Run(() => {
				return command.ExecuteNonQuery();
			});

			return rows == 1;

		}

		public async Task<bool> RemoveKey(byte[] key) {

			var command = DBConnection.CreateCommand();
			command.CommandText = "DELETE FROM data WHERE key = $key;";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;

			int rows = await Task<int>.Run(() => {
				return command.ExecuteNonQuery();
			});

			return 1 == rows;
		}

		public async Task<byte[]> SelectData(string dataCol, string conditionCol, byte[] conditionVal) {

			var command = DBConnection.CreateCommand();
			command.CommandText = $"SELECT {dataCol} FROM data WHERE {conditionCol} = $conditionalVal;";
			command.Parameters.Add("$conditionalVal", SqliteType.Blob, conditionVal.Length).Value = conditionVal;

			byte[] data = null;

			await Task.Run(() => {
				using (var reader = command.ExecuteReader()) {
					if (!reader.HasRows) return;
					reader.Read();
					data = (byte[])reader.GetValue(0);
				}
			});

			return data;
		
		}	

		public async Task<List<PeerInfo>> GetPeers() {
			
			List<PeerInfo> peers = new List<PeerInfo>(); 

			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT host, port FROM peers;";

			await Task.Run(() => {
				using (var reader = command.ExecuteReader()) {
					while (reader.Read()) {
						var host = reader.GetString(0);
						var port = reader.GetInt32(1);
						PeerInfo peer = new PeerInfo(host, port);
						peers.Add(peer);
					}
				}
			});

			return peers;

		}

		public async Task<bool> InsertPeer(PeerInfo newPeer) {

			var command = DBConnection.CreateCommand();
			command.CommandText = @"
						INSERT INTO peers (host, port)
						SELECT $host, $port
						WHERE NOT EXISTS (SELECT 1 FROM peers WHERE host = $host AND port = $port) 
						";
			command.Parameters.AddWithValue("$host", newPeer.Host);
			command.Parameters.AddWithValue("$port", newPeer.Port);

			int rows = await Task<int>.Run(() => {
				return command.ExecuteNonQuery();
			});
			
			return 1 == rows;
		}

		public async Task<bool> RemovePeer(PeerInfo peer) {

			var command = DBConnection.CreateCommand();
			command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
			command.Parameters.AddWithValue("$host", peer.Host);
			command.Parameters.AddWithValue("$port", peer.Port);

			int rows = await Task<int>.Run(() => {
				return command.ExecuteNonQuery();
			});
			
			return 1 == rows;
		}

		public async Task<bool> ContainsPeer(PeerInfo peer) {
			
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT * FROM peers WHERE host = $host AND port = $port";
			command.Parameters.AddWithValue("$host", peer.Host);
			command.Parameters.AddWithValue("$port", peer.Port);
	
			bool exists = false;
			await Task<int>.Run(() => {
				using(var reader = command.ExecuteReader()) {
					exists = reader.HasRows;
				}
			});

			return exists;

		}

		// public void BlackListPeer(Peer badPeer) { } 

		public void Close() {
			DBConnection.Close();
		}

	}
}
