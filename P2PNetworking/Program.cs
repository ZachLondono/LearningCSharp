﻿using Microsoft.Data.Sqlite;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace P2PNetworking {
    class Peer {

        static void Main(string[] args) {               
            Peer peer = new Peer();
            peer.ConnectToPeers();

            Thread listenerThread = new Thread(new ThreadStart(peer.ListenForConnections));

            listenerThread.Start();

        }

        private SqliteConnection DBConnection { get; set; }
        private List<Socket> Connections { get; set;}
        private readonly int Port;
        private readonly int Backlog;

        public Peer() : this(11000, 10) {            
            // Start on default port
        }

        public Peer(int port, int backlog) {
 
            Port = port;
            Backlog = backlog;
            
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

        /// Begins listening for incoming connections
        public void ListenForConnections() {

            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress iPAddress= ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(iPAddress, Port);

			Socket listener  = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try {

                listener.Bind(localEndPoint);
                listener.Listen(Backlog);

                while (true) {

                    Socket handler = listener.Accept();

                    AddPeer(handler);

                }

            } catch (Exception e) {

                Console.Write("Unable to listen for connections " + e.ToString());

            }

        }

        /// Connects to all known peers stored in local database
        public void ConnectToPeers() {
            
            Console.WriteLine("Attempting to Connect to Known Peers");

            // Read all peer entries from table
            var command = DBConnection.CreateCommand();
            command.CommandText = 
            @"
                SELECT host, port FROM peers;
            ";


            using (var reader = command.ExecuteReader()) {

                while (reader.Read()) {                        
                    var host = reader.GetString(0);
                    var port = reader.GetInt32(1);
                    Console.WriteLine("Attempting to Connect to Peer: {0}:{1}", host, port);

                    try {
                        
                        // Connect to new peer
                        AddPeer(host, port);
                        Console.WriteLine("Successful Connection to Peer: {0}:{1}", host, port);

                    } catch (Exception e) {
                        // Error connecting to new peer, remove it from peers list

                        command = DBConnection.CreateCommand();
                        command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
                        command.Parameters.AddWithValue("$host", host);
                        command.Parameters.AddWithValue("$port", port);

                        int row = command.ExecuteNonQuery();
                        Console.WriteLine($"Removed peer {host}:{port} for failing to connect");

                    }

                }

            }
        }


        public void AddPeer(Socket socket) {

            // If no exception is thrown, add connection to list of connections
            Connections.Add(socket);
 
        }

        public void AddPeer(string host, int port) {
            
            // Get information about the host
            IPHostEntry ipHostInfo = Dns.GetHostEntry(host); 
            IPAddress ipAddress = ipHostInfo.AddressList[0];  
            IPEndPoint remoteEP = new IPEndPoint(ipAddress,port);  

            // Create a socket to connect to remote host
            Socket peerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Attempt to connect
            peerSocket.Connect(remoteEP);

            // If no exception is thrown, add connection to list of connections
            AddPeer(peerSocket);
        
        }

        public void SavePeer(string host, int port) {

            // store host and port in database, if they do not already exist
            SqliteCommand check = DBConnection.CreateCommand();

            // Check if peer already exists in table
            check.CommandText = "SELECT 1 FROM peers WHERE host = $host AND port = $port;";
	        check.Parameters.AddWithValue("$host", host);
            check.Parameters.AddWithValue("$port", port);

            using (var reader = check.ExecuteReader()) {
                // If any rows are returned, it exists
                if (reader.HasRows) return;
            }

            // Insert new peer into table
            SqliteCommand insert = DBConnection.CreateCommand();
            insert.CommandText = "INSERT INTO peers (host, port) VALUES ($host, $port);";
	        insert.Parameters.AddWithValue("$host", host);
            insert.Parameters.AddWithValue("$port", port);

            int rows = insert.ExecuteNonQuery();
            if (rows < 1) Console.Write("Error saving new peer");

        }

    }

}
