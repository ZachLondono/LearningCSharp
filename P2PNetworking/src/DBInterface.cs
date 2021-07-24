using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace P2PNetworking {

	public struct Data {
		byte[] key;
		byte[] value;
	}

	public struct Peer {
		public string Host { get; }
		public int Port { get; }

		public Peer(string host, int port) {
			Host = host;
			Port = port;
		}

	}

	public class DBInterface {
	
        private SqliteConnection DBConnection { get; }

		public DBInterface(string storageDirectory) {

			// Verify that storage directory is a valid directory
			bool exists = System.IO.Directory.Exists(storageDirectory);

			if (!exists) throw new System.ArgumentException("Invalid storage directory, can't connect to database");

			DBConnection = new SqliteConnection($"Data Source={storageDirectory}/Peer.db");
			DBConnection.Open();

		}

		private void CreateTableIfNotExist(string name, string[] columns) {

			var queryString = $"CREATE IF NOT EXISTS {name} (";
			for (int i = 0; i < columns.Length; i++) {
				queryString += columns[i];
				if (i < columns.Length - 1) queryString += ",\n";
			}
			queryString += "\n)";

			var command = DBConnection.CreateCommand();
			command.CommandText = queryString;

			command.ExecuteNonQueryAsync();

		}

		public bool ContainsKey(byte[] key) {
			
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT * WHERE key = $key";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
			
			var reader = command.ExecuteReader();

			return reader.HasRows;

		}

		public bool InsertPair(byte[] key, byte[] value) {

			// Inserts the key value pair, there should only be one instance of key
			// NOTE: this is likely susceptible to duplicate keys in the case of a race condition

			var command = DBConnection.CreateCommand();
			command.CommandText = @"BEGIN
									IF NOT EXISTS (SELECT * FROM data WHERE key = $key)
									BEGIN
										INSERT INTO data (key, value) VALUES ( $key, $value )
										END
									END;";
			command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
			command.Parameters.Add("$value", SqliteType.Blob, value.Length).Value = value;
			
			// insertion was successful if 1 row was changed
			int rows = command.ExecuteNonQuery();
			if (rows != 1) 
				Node.WriteLine($"ERROR inserting pair. Row altered: {rows}");

			return rows == 1;

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
			command.CommandText = "INSERT INTO peers (host, port) VALUES ( $host, $port);";
			command.Parameters.AddWithValue("$host", newPeer.Host);
			command.Parameters.AddWithValue("$port", newPeer.Port);

			return 1 == command.ExecuteNonQuery();

		}

		public bool RemovePeer(Peer peer) {

			var command = DBConnection.CreateCommand();
			command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
			command.Parameters.AddWithValue("$host", peer.Host);
			command.Parameters.AddWithValue("$port", peer.Port);

			return 1 == command.ExecuteNonQuery();

		}

		// public void BlackListPeer(Peer badPeer) { } 

	}

}