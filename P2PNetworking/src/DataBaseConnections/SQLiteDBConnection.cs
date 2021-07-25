using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace P2PNetworking {

	public class SQLiteDBConnection : IDBInterface  {
	
		private SqliteConnection DBConnection { get; }

		public SQLiteDBConnection(string? storageDirectory) {

			System.Console.WriteLine("Starting SQLite DB");

			if (storageDirectory != null) {
				// Verify that storage directory is a valid directory
				bool exists = System.IO.Directory.Exists(storageDirectory);
	
				if (!exists) throw new System.ArgumentException("Invalid storage directory, can't connect to database");
	
				DBConnection = new SqliteConnection($"Data Source={storageDirectory}/Peer2Peer.db");
			} else {
				DBConnection = new SqliteConnection("Data Source=Peer2Peer.db");
			}

			DBConnection.Open();

			System.Console.WriteLine("Creating peer Table");
			CreateTableIfNotExist("peers", new string[]{"host VARCHAR(255)", "port INTEGER"});
			System.Console.WriteLine("Creating data Table");
			CreateTableIfNotExist("data", new string[]{"key BLOB", "value BLOB"});
		}

		public void CreateTableIfNotExist(string name, string[] columns) {

			var queryString = $"CREATE TABLE IF NOT EXISTS {name} \n(\n";
			for (int i = 0; i < columns.Length; i++) {
				queryString += "\t" + columns[i];
				if (i < columns.Length - 1) queryString += ",\n";
			}
			queryString += "\n);";

			//System.Console.WriteLine($"SQL Query: \"{queryString}\"");

			var command = DBConnection.CreateCommand();
			command.CommandText = queryString;

			command.ExecuteNonQuery();

		}

		public bool ContainsKey(byte[] key) {
		
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT * FROM data WHERE key = $key";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
			
			var reader = command.ExecuteReader();

			return reader.HasRows;

		}

		public bool InsertPair(DataPair pair) {

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
			
			// insertion was successful if 1 row was changed
			int rows = command.ExecuteNonQuery();

			return rows == 1;

		}

		public bool RemoveKey(byte[] key) {

			var command = DBConnection.CreateCommand();
			command.CommandText = "DELETE FROM data WHERE key = $key;";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;

			int rows = command.ExecuteNonQuery();
			return 1 == rows;
		}

		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal) {
	
			// Returns data from dataCol where conditionCol = ConditionVal
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT $dataCol FROM data WHERE $conditionCol = $conditionalVal;";
			command.Parameters.AddWithValue("$dataCol",dataCol);
			command.Parameters.AddWithValue("$conditionCol",conditionCol);
			command.Parameters.Add("$conditionVal", SqliteType.Blob, conditionVal.Length).Value = conditionVal;

			using (var reader = command.ExecuteReader()) {
				// READ BYTES
			}

			return null;
		}	

		public List<Peer> GetPeers() {
			
			List<Peer> peers = new List<Peer>(); 

			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT host, port FROM peers;";

			using (var reader = command.ExecuteReader()) {
				while (reader.Read()) {
					var host = reader.GetString(0);
					var port = reader.GetInt32(1);
					Peer peer = new Peer(host, port);
					peers.Add(peer);
				}
			}

			return peers;

		}

		public bool InsertPeer(Peer newPeer) {

			var command = DBConnection.CreateCommand();
			command.CommandText = @"
						INSERT INTO peers (host, port)
						SELECT $host, $port
						WHERE NOT EXISTS (SELECT 1 FROM peers WHERE host = $host AND port = $port) 
						";
			command.Parameters.AddWithValue("$host", newPeer.Host);
			command.Parameters.AddWithValue("$port", newPeer.Port);

			int rows = command.ExecuteNonQuery();
			return 1 == rows;
		}

		public bool RemovePeer(Peer peer) {

			var command = DBConnection.CreateCommand();
			command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
			command.Parameters.AddWithValue("$host", peer.Host);
			command.Parameters.AddWithValue("$port", peer.Port);

			return 1 == command.ExecuteNonQuery();

		}

		public bool ContainsPeer(Peer peer) {
			
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT * FROM peers WHERE host = $host AND port = $port";
			command.Parameters.AddWithValue("$host", peer.Host);
			command.Parameters.AddWithValue("$port", peer.Port);
			
			var reader = command.ExecuteReader();

			return reader.HasRows;

		}

		// public void BlackListPeer(Peer badPeer) { } 

	}

}
