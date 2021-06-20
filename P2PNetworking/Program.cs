using Microsoft.Data.Sqlite;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace P2PNetworking {
    class Peer {

        static void Main(string[] args) {               
            Peer peer = new Peer();
        }

        private SqliteConnection DBConnection { get; set; }
        private List<Socket> Connections { get; set;}

        public Peer() {

            Console.WriteLine("Starting Peer...");

            // List to store connections
            Connections = new List<Socket>();
            
            Console.WriteLine("Opening Connection To Database...");
            // Open new connection to database
            DBConnection = new SqliteConnection("Data Source=Peer.db");
            DBConnection.Open();

            // If the peers table does not exist (first time stating this peer), create it
            var command = DBConnection.CreateCommand();
            command.CommandText = 
            @"
                CREATE TABLE IF NOT EXISTS peers (
                    host VARCHAR(100),
                    port INTEGER
                );
            ";
            Console.WriteLine("Creating peers Table if it does not yet exist");
            command.ExecuteNonQuery();
            
        
        }

        public void ConnectToPeers() {
            
            Console.WriteLine("Attempting to Connect to Known Peers");

            // Read all peer entries from table
            command = DBConnection.CreateCommand();
            command.CommandText = 
            @"
                SELECT host, port FROM peers;
            ";


            using (var reader = command.ExecuteReader()) {

                while (reader.Read()) {                        
                    var host = reader.GetString(0);
                    var port = reader.GetInt32(1);
                    Console.WriteLine("Attempting to Connect to Peer: {0}:{1}", host, port);

                    IPHostEntry ipHostInfo = Dns.GetHostEntry(host); 
				    IPAddress ipAddress = ipHostInfo.AddressList[0];  
				    IPEndPoint remoteEP = new IPEndPoint(ipAddress,port);  

				    Socket peerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    try {
                        
                        // Connect to new peer
                        peerSocket.Connect(remoteEP);
                        Console.WriteLine("Successful Connection to Peer: {0}:{1}", host, port);
                        Connections.Add(peerSocket);

                    } catch (Exception e) {
                        // Error connecting to new peer, remove it from peers list

                        command = DBConnection.CreateCommand();
                        command.CommandText = 
                        @"
                            DELETE FROM peers WHERE host = $host AND port = $port;
                        ";
                        command.Parameters.AddWithValue("$host", host);
                        command.Parameters.AddWithValue("$port", port);

                        int row = command.ExecuteNonQuery();
                        Console.WriteLine($"Removed peer {host}:{port} for failing to connect");

                    }

                }

            }
        }

    }

}
