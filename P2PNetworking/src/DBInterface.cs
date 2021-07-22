using Microsoft.Data.Sqlite;

namespace P2PNetworking {

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

		public bool ContainsKey(byte key) {
			return false;			
		}

		public void InsertPair(byte key, byte value) {
			// Inserts the key value pair, there should only be one instance of key
		}

		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal) {
	
			// Returns data from dataCol where conditionCol = ConditionVal
			var command = DBConnection.CreateCommand();
			command.CommandText = "SELECT $dataCol FROM data WHERE $conditionCol = $conditionalVal";
			command.Parameters.AddWithValue("$dataCol",dataCol);
			command.Parameters.AddWithValue("$conditionCol",conditionCol);
			command.Parameters.Add("$conditionVal", SqliteType.Blob, conditionVal.Length).Value = conditionVal;

			return null;
		}	

		// public Peer[] GetPeers() {}

		// public void InsertPeer() {}

		// public void RemovePeer() {}

		// public void BlackListPeer() {} 

	}

}